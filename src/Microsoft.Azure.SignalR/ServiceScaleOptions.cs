using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    public class ServiceScaleOptions
    {
        public int ServerCount { get; set; }

        public ServiceEndpoint Endpoint { get; set;}

        // 0 - add
        // 1 - remove
        // 2 - update
        public int OperationType { get; set; }
    }
}
