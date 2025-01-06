using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WitShells.Server.Udp
{
    public abstract class UdpListenerBase : IDisposable
    {
        protected struct UdpRef
        {
            public UdpClient Client;
            public CancellationTokenSource TokenSource;
            public bool IsRunning => Client.Client.Connected;
            public bool IsCancelled => IsRunning && !TokenSource.Token.IsCancellationRequested;
        }

        protected abstract int Port { get; }
        protected abstract string ServerAddress { get; }

        private ConcurrentDictionary<int, UdpRef> _udpClients = new ConcurrentDictionary<int, UdpRef>();
        private ConcurrentQueue<Action> _receiveEvents = new ConcurrentQueue<Action>();
        private ConcurrentQueue<string> _sendEvents = new ConcurrentQueue<string>();

        private Thread _receiveThread;
        private Thread _sendThread;


        public UdpClient Client
        {
            get
            {
                if (_udpClients.TryGetValue(0, out UdpRef udpRef))
                {
                    return udpRef.Client;
                }
                else
                {
                    var client = new UdpClient();
                    var tokenSource = new CancellationTokenSource();
                    _udpClients.TryAdd(0, new UdpRef { Client = client, TokenSource = tokenSource });
                    return client;
                }
            }
        }

        public void Start()
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(ServerAddress), Port);
            Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Client.Client.Bind(endPoint);
        }


        private void HandleIncoming()
        {

        }

        private void HandleOutgoing()
        {

        }

        public void Dispose()
        {
        }
    }
}