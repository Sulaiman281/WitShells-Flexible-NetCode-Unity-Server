using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace WitShells.Server.Tcp
{
    public abstract class AbstractTcpServer : MonoSingleton<AbstractTcpServer>, IDisposable
    {
        [Range(0, 25)][SerializeField] private float _pingInterval = 5f;

        public abstract ushort Port { get; }
        public abstract string ServerAddress { get; }
        private Thread _serverThread;
        private ConcurrentQueue<Action> _receiveEvents = new ConcurrentQueue<Action>();
        private ConcurrentDictionary<uint, TcpClientHandle> _clients = new ConcurrentDictionary<uint, TcpClientHandle>();
        private ConcurrentDictionary<int, TcpListener> _listeners = new ConcurrentDictionary<int, TcpListener>();
        private ConcurrentDictionary<int, bool> _isRunning = new ConcurrentDictionary<int, bool>();
        public int TotalClients => _clients.Count;

        private uint _successfulConnections = 0;

        private float _currentPingInterval = 0f;

        public TcpListener Listener
        {
            get
            {
                if (_listeners.TryGetValue(0, out TcpListener listener))
                {
                    return listener;
                }
                else
                {
                    var serverIPAddress = IPAddress.Parse(ServerAddress);
                    var newListener = new TcpListener(serverIPAddress, Port);
                    _listeners.TryAdd(0, newListener);
                    return newListener;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                if (_isRunning.TryGetValue(0, out bool isRunning))
                {
                    return isRunning;
                }
                else
                {
                    _isRunning.TryAdd(0, false);
                }

                return false;
            }
            private set
            {
                _isRunning.TryUpdate(0, value, !value);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (_receiveEvents.TryDequeue(out Action action))
            {
                action?.Invoke();
            }

            // client side updates
            foreach (var client in _clients)
            {
                if (client.Value.IsConnected.TryDequeue(out bool isConnected))
                {
                    if (isConnected)
                    {
                        OnClientConnected(client.Key);
                    }
                }

                if (client.Value.ReceiveMessages.TryDequeue(out string msg))
                {
                    OnMessageReceived(client.Key, msg);
                }

                if (client.Value.IsDisconnected.TryDequeue(out bool isDisconnected))
                {
                    if (isDisconnected)
                    {
                        _clients.TryRemove(client.Key, out var ch);
                        OnClientDisconnected(client.Key);
                    }
                }
            }
        }

        protected virtual void Update()
        {
            PingClients();
        }

        protected virtual void OnApplicationQuit()
        {
            StopServer();
        }


        private void PingClients()
        {
            _currentPingInterval += Time.fixedDeltaTime;
            if (_currentPingInterval >= _pingInterval)
            {
                SendMessageToAllClients("ping");
                _currentPingInterval = 0f;
            }
        }
#if UNITY_EDITOR || UNITY_STANDALONE

        private bool IsPortInUse(int port)
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
            {
                if (tcpi.LocalEndPoint.Port == port)
                {
                    return true;
                }
            }
            return false;
        }

        private void TerminateProcessOnPort(int port)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netstat -ano | findstr :{port}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd(); string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries); foreach (var line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 4)
                {
                    int pid = int.Parse(parts[4]);
                    try
                    {
                        System.Diagnostics.Process.GetProcessById(pid).Kill(); Console.WriteLine($"Process with PID {pid} using port {port} has been terminated.");
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Failed to terminate process with PID {pid}. Exception: {ex.Message}");
                    }
                }
            }
        }

#endif

        public virtual void StartServer()
        {
            try
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                if (IsPortInUse(Port))
                {
                    TerminateProcessOnPort(Port);
                    Invoke(nameof(StartServer), 2);
                    return;
                }
#endif

                _receiveEvents.Clear();
                _clients.Clear();

                Listener.Start();
                IsRunning = true;

                _serverThread = new Thread(HandleIncomingMessages);
                _serverThread.Start();

                // call abstract method
                OnServerStarted();
            }
            catch (Exception e)
            {
                OnServerFailed();
                Debug.LogError("Server Failed To Started Reason Below\n" + e);
            }

        }

        public virtual void StopServer()
        {
            // close all clients
            foreach (var client in _clients)
            {
                client.Value.Close();
            }

            IsRunning = false;

            Listener.Stop();
            _listeners.Clear();
            // call abstract method
            QueueReceiveEvent(OnServerStopped);
        }

        private async void HandleIncomingMessages()
        {
            try
            {
                while (IsRunning)
                {
                    if (Listener.Server == null || !Listener.Server.IsBound)
                    {
                        Debug.Log("Server is not bound");
                        break;
                    }

                    // check for new connections
                    if (Listener.Server.Poll(0, SelectMode.SelectRead))
                    {
                        Debug.Log("New Connection");
                    }

                    TcpClient client = await Listener.AcceptTcpClientAsync();

                    if (client != null)
                    {
                        AddClient(client);
                    }
                }
                QueueReceiveEvent(OnServerStopped);
            }
            finally
            {
                StopServer();
            }
        }

        private void AddClient(TcpClient client)
        {
            uint id = _successfulConnections + 1;
            TcpClientHandle clientHandle = new TcpClientHandle(client);

            _clients.TryAdd(id, clientHandle);
            _successfulConnections++;
        }

        private void QueueReceiveEvent(Action action)
        {
            _receiveEvents.Enqueue(action);
        }

        public void SendMessageToAllClients(string message)
        {
            foreach (var client in _clients)
            {
                SendMessageToClient(client.Key, message);
            }
        }

        public void SendMessageToClient(uint clientId, string message)
        {
            if (_clients.TryGetValue(clientId, out TcpClientHandle client))
            {
                client.SendMessage(message);
            }
            else
            {
                Debug.LogWarning($"Client with id {clientId} not found");
            }
        }

        public void DisconnectClient(uint clientId)
        {
            if (_clients.TryGetValue(clientId, out TcpClientHandle client))
            {
                client.Close();
                _clients.TryRemove(clientId, out var ch);
            }
            else
            {
                Debug.LogWarning($"Client with id {clientId} not found");
            }
        }

        public void Dispose()
        {
            StopServer();
        }

        // abstract method to drive
        protected abstract void OnServerStarted();
        protected abstract void OnClientConnected(uint clientId);
        protected abstract void OnClientDisconnected(uint clientId);
        protected abstract void OnServerStopped();
        protected abstract void OnServerFailed();
        protected abstract void OnMessageReceived(uint clientId, string message);
    }
}