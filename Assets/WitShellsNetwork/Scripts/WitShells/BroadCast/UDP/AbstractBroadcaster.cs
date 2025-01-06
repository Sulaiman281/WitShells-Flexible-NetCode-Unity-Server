using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace WitShells.BroadCast.Udp
{
    public abstract class AbstractBroadcaster : MonoBehaviour, IDisposable
    {
        [Header("Settings")]
        [Range(1f, 10f)][SerializeField] private float broadcastWaitTime = 2f;

        private ConcurrentQueue<Action> _receiveEvents = new ConcurrentQueue<Action>();
        private ConcurrentDictionary<int, UdpClient> _clients = new ConcurrentDictionary<int, UdpClient>();

        private ConcurrentDictionary<int, CancellationTokenSource> _cancelTokens = new ConcurrentDictionary<int, CancellationTokenSource>();

        // properties
        public UdpClient Client
        {
            get
            {
                if (_clients.TryGetValue(0, out UdpClient client))
                {
                    return client;
                }
                else
                {
                    var newClient = new UdpClient();
                    _clients.TryAdd(0, newClient);
                    return newClient;
                }
            }
        }

        #region  Mono Cycle

        protected virtual void Start()
        {
            Client.EnableBroadcast = true;
        }

        protected virtual void Update()
        {
            while (_receiveEvents.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Action Fail to process: {e}");
                }
            }
        }

        protected virtual void OnApplicationQuit()
        {
            Shutdown();
        }

        #endregion

        protected void Shutdown()
        {
            Client.Close();

            // stop all threads
            foreach (var cancelToken in _cancelTokens)
            {
                cancelToken.Value.Cancel();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        #region Handling Broadcast Message

        public void SendBroadcastMessage(ushort port, string message)
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Broadcast, port);
            SendMessage(message, remoteEndPoint);
        }

        private void SendMessage(string address, IPEndPoint remoteEndPoint)
        {
            try
            {
                Debug.Log($"Sending message to {remoteEndPoint}");
                var data = System.Text.Encoding.UTF8.GetBytes(address);
                Client.Send(data, data.Length, remoteEndPoint);

                var cancelToken = new CancellationTokenSource();
                var thread = new Thread(() => WaitingForResponse(remoteEndPoint, cancelToken.Token));
                thread.Start();

                AddPortCancelToken(remoteEndPoint.Port, cancelToken);

                // stop the thread after broadcastWaitTime seconds
                Task.Delay(TimeSpan.FromSeconds(broadcastWaitTime)).ContinueWith(_ =>
                {
                    cancelToken.Cancel();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
        }

        private void AddPortCancelToken(int port, CancellationTokenSource cancelToken)
        {
            // if the port is already in the dictionary, cancel the token
            if (_cancelTokens.TryGetValue(port, out CancellationTokenSource token))
            {
                token.Cancel();

                // remove the port from the dictionary
                _cancelTokens.TryRemove(port, out CancellationTokenSource _);
            }

            _cancelTokens.TryAdd(port, cancelToken);
        }

        private async void WaitingForResponse(IPEndPoint remoteEndPoint, CancellationToken token = default)
        {
            try
            {
                using (token.Register(() => Client.Close()))
                {
                    var receiveTask = Client.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(TimeSpan.FromSeconds(broadcastWaitTime), token));

                    if (completedTask == receiveTask && !token.IsCancellationRequested)
                    {
                        var result = receiveTask.Result;
                        var message = System.Text.Encoding.UTF8.GetString(result.Buffer);
                        OnBroadcastReceivedFromPort((uint)remoteEndPoint.Port, message);
                    }
                    else
                    {
                        TimeUpForPort((uint)remoteEndPoint.Port);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
                OnBroadcastFailedToReceive((uint)remoteEndPoint.Port);
            }
        }


        #endregion

        #region Abstract Methods
        protected abstract void OnBroadcastReceivedFromPort(uint port, string message);
        protected abstract void TimeUpForPort(uint port);
        protected abstract void OnBroadcastFailedToReceive(uint port);

        #endregion
    }
}