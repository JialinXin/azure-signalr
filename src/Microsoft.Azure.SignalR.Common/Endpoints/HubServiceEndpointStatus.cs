using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpointStatus
    {
        public int OnlineServerCount { get; set; }
        public string OnlineServerHash { get; set; }

        public HubServiceEndpointStatus(string pingMessage)
        {
            var items = pingMessage.Split(':');
            OnlineServerCount = int.Parse(items[0]);
            OnlineServerHash = items[1];
        }
    }
}
