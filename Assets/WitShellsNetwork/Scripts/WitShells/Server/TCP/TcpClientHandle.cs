using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace WitShells.Server.Tcp
{

    public class TcpClientHandle
    {
        struct ClientRef
        {
            public int id;
            public TcpClient client;
            public CancellationTokenSource cancelToken;

            public readonly bool IsConnected => client.Connected;
            public readonly bool Connected => client.Connected && !cancelToken.Token.IsCancellationRequested;

            public readonly bool IsNull()
            {
                return client == null;
            }

            public readonly void Close()
            {
                if (client != null)
                {
                    client.Close();
                }
                cancelToken.Cancel();
            }
        }

        private ConcurrentQueue<string> _receiveMessages = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _sendEvents = new ConcurrentQueue<string>();
        private ConcurrentQueue<bool> _isConnected = new ConcurrentQueue<bool>();
        private ConcurrentQueue<bool> _isDisconnected = new ConcurrentQueue<bool>();

        private ConcurrentDictionary<int, ClientRef> _client = new ConcurrentDictionary<int, ClientRef>();

        private Thread _clientThread;
        private Thread _sendThread;
        private CancellationTokenSource _cancelToken = new CancellationTokenSource();

        private ClientRef Client
        {
            get
            {
                if (_client.TryGetValue(0, out ClientRef client))
                {
                    return client;
                }
                return default;
            }
            set
            {
                _client.TryAdd(0, value);
            }
        }

        public ConcurrentQueue<string> ReceiveMessages => _receiveMessages;
        public ConcurrentQueue<bool> IsConnected => _isConnected;
        public ConcurrentQueue<bool> IsDisconnected => _isDisconnected;


        public TcpClientHandle(TcpClient client)
        {
            _cancelToken = new CancellationTokenSource();
            Client = new ClientRef
            {
                id = 0,
                client = client,
                cancelToken = _cancelToken
            };

            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            _clientThread = HandleIncoming();
            _sendThread = HandleOutgoing();
        }

        private Thread HandleIncoming()
        {
            Thread thread = new Thread(() =>
            {
                using NetworkStream stream = Client.client.GetStream();
                using StreamReader reader = new StreamReader(stream);
                try
                {
                    _isConnected.Enqueue(true);
                    while (Client.Connected)
                    {
                        try
                        {
                            string message = reader.ReadLine();

                            if (string.Equals("pong", message))
                            {
                                continue;
                            }

                            if (!string.IsNullOrEmpty(message))
                            {
                                _receiveMessages.Enqueue(message);
                            }
                        }
                        catch (IOException)
                        {
                            // ignore exception
                            break;
                        }

                    }

                    _isDisconnected.Enqueue(true);

                }
                catch (Exception)
                {
                    // handle exception
                }
                finally
                {
                    Close();
                }
            });

            thread.Start();
            return thread;
        }

        private Thread HandleOutgoing()
        {
            Thread thread = new Thread(() =>
            {
                using NetworkStream stream = Client.client.GetStream();
                using StreamWriter writer = new StreamWriter(stream);
                writer.AutoFlush = true;
                try
                {
                    // send welcome message
                    while (Client.Connected)
                    {
                        while (_sendEvents.TryDequeue(out string message))
                        {
                            try
                            {
                                writer.WriteLine(message);
                            }
                            catch (IOException e)
                            {
                                // handle exception
                                Debug.LogWarning(e.Message);
                                Close();
                                break;
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // handle exception
                }
                finally
                {
                    Close();
                }
            });

            thread.Start();
            return thread;
        }

        public void SendMessage(string message)
        {
            _sendEvents.Enqueue(message);
        }

        public void Close()
        {
            if (!Client.IsNull())
            {
                Client.Close();
            }
            _client.Clear();
        }
    }
}