using System;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Startup
{
    internal class NegotiateHandler2
    {
        private static readonly Type _negotiateHandlerType = typeof(ServiceOptions).Assembly.GetType("Microsoft.Azure.SignalR.NegotiateHandler");
        private static readonly MethodInfo _processNegotiate = _negotiateHandlerType.GetMethod("Process");

        private readonly object _negotiateHandler;

        public NegotiateHandler2(IServiceProvider serviceProvider)
        {
            _negotiateHandler = serviceProvider.GetService(_negotiateHandlerType);
        }

        public NegotiationResponse Process(HttpContext httpContext, string hubName)
        {
            return (NegotiationResponse)_processNegotiate.Invoke(_negotiateHandler, new object[] { httpContext, hubName });
        }
    }
}
