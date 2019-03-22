using System;
using System.Reflection;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Startup
{
    public static class AzureSignalRConnectionBuilderExtensions
    {
        private static readonly MethodInfo _useHubMethod = typeof(SignalRConnectionBuilderExtensions).GetMethod(nameof(SignalRConnectionBuilderExtensions.UseHub));

        // A late bount version of UseHub<T>
        public static IConnectionBuilder UseHub(this IConnectionBuilder builder, Type hubType)
        {
            return (IConnectionBuilder)_useHubMethod.MakeGenericMethod(hubType).Invoke(null, new object[] { builder });
        }
    }
}
