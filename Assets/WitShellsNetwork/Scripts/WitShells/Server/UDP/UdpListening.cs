using UnityEngine;
using UnityEngine.Events;

namespace WitShells.Server.Udp
{
    public class UdpListening : AbstractUdpListener
    {
        public override int Port { get; }

        [Header("Events")]
        public UnityEvent<string> onMessageReceived;

        protected override void OnConnectionClosed()
        {
            Debug.Log("Connection closed");
        }

        protected override void OnConnectionOpen()
        {
            Debug.Log("Connection opened on port: " + Port);
        }

        protected override void OnMessageReceived(string message)
        {
            Debug.Log("Received: " + message);
        }
    }
}