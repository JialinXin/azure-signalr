using ServicePingMessage = Microsoft.Azure.SignalR.Protocol.PingMessage;

namespace Microsoft.Azure.SignalR
{
    public class ShutdownEndpointPingMessage
    {
        private const string Key = "shutdown";

        public static ServicePingMessage GetPingMessage()
        {
            return new ServicePingMessage { Messages = new[] { Key, "self" } };
        }
    }
}
