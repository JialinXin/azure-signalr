using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.Startup
{
    public class HubMetadata
    {
        public HubMetadata(Type hubType)
        {
            HubType = hubType;
        }

        public Type HubType { get; }
    }
}
