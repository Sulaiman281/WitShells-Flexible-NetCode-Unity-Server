using UnityEngine;
using UnityEngine.Events;

namespace WitShells.BroadCast.Udp
{
    public class BroadcastUdp : AbstractBroadcaster
    {
        [Header("Events")]
        public UnityEvent<uint, string> onBroadcastReceived;
        public UnityEvent<uint> onBroadCastFailedToReceive;
        public UnityEvent<uint> onBroadCastTimeUp;


        protected override void OnBroadcastFailedToReceive(uint port)
        {
            onBroadCastFailedToReceive.Invoke(port);
        }

        protected override void OnBroadcastReceivedFromPort(uint port, string message)
        {
            onBroadcastReceived.Invoke(port, message);
        }

        protected override void TimeUpForPort(uint port)
        {
            onBroadCastTimeUp.Invoke(port);
        }
    }
}