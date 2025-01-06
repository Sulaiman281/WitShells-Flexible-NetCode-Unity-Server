using UnityEngine;
using UnityEngine.Events;

namespace WitShells.Server.Tcp
{
    public class ServerTcp : AbstractTcpServer
    {
        [Header("Settings")]
        public override ushort Port => NetworkSettings.Instance.tcpPort;
        public override string ServerAddress => NetworkSettings.Instance.ServerAddress;


        [Header("Events")]
        public UnityEvent<uint> onClientConnected;
        public UnityEvent<uint> onClientDisconnected;
        public UnityEvent<uint, string> onMessageReceived;


#if UNITY_EDITOR
        public bool debugMode;
        void OnGUI()
        {
            if (!debugMode) return;
            if (IsRunning)
            {
                if (GUI.Button(new Rect(100, 50, 200, 100), "Stop Server"))
                {
                    StopServer();
                }
            }
            else
            {
                if (GUI.Button(new Rect(100, 50, 200, 100), "Start Server"))
                {
                    StartServer();
                }
            }
        }

#endif


        #region Mono Cycle




        #endregion

        protected override void OnClientConnected(uint clientId)
        {
            Debug.Log($"Client {clientId} connected");

            // if connection reaches to max client count, disconnect the client
            if (TotalClients > NetworkSettings.Instance.maxConnections)
            {
                DisconnectClient(clientId);
                return;
            }

            onClientConnected?.Invoke(clientId);
        }

        protected override void OnClientDisconnected(uint clientId)
        {
            onClientDisconnected?.Invoke(clientId);
        }

        protected override void OnMessageReceived(uint clientId, string message)
        {
            Debug.Log($"Received message from client {clientId}: {message}");
            onMessageReceived?.Invoke(clientId, message);
        }

        protected override void OnServerFailed()
        {
            Debug.LogError("Server failed");
        }

        protected override void OnServerStarted()
        {
            Debug.Log("Server started on port " + Port);
        }

        protected override void OnServerStopped()
        {
            Debug.Log("Server stopped");
        }
    }
}