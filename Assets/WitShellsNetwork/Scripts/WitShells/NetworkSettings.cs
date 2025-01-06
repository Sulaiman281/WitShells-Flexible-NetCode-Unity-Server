using System.Net;
using System.Net.Sockets;
using UnityEngine;
using WitShells.Enums;

namespace WitShells
{
    [CreateAssetMenu(fileName = "NetworkSettings", menuName = "Scriptable Objects/NetworkSettings")]
    public class NetworkSettings : ScriptableObject
    {
        private static NetworkSettings instance;

        public static NetworkSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<NetworkSettings>("NetworkSettings");

                    instance.serverAddress = instance.GetIPAddress();
                }

                return instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private ServerType serverType;
        [SerializeField] private string serverAddress;

        [Header("UDP Connection Settings")]

        [Header("Tcp Connection Settings")]
        public int maxConnections = 2;
        public ushort tcpPort = 9901;

        public string ServerAddress
        {
            get
            {
                if (serverType == ServerType.LOCAL)
                {
                    return GetIPAddress();
                }

                if (string.IsNullOrEmpty(serverAddress))
                {
                    Debug.LogError("Server Address is not set. Please set the server address.");
                }

                return serverAddress;
            }
        }



        public void SaveIdentity()
        {
            PlayerPrefs.Save();
        }

        public bool HasIdentity(ref string identity)
        {
            if (PlayerPrefs.HasKey("UniqueIdentifier"))
            {
                identity = PlayerPrefs.GetString("UniqueIdentifier");
                return true;
            }
            return false;
        }

        private string GetIPAddress()
        {
            string ipAddress = string.Empty;
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ip.ToString();
                    break;
                }
            }
            return ipAddress;
        }

    }
}