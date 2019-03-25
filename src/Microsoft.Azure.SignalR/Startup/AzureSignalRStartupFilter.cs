using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.Startup
{
#if NETCOREAPP3_0
    using Microsoft.AspNetCore.Http.Endpoints;
#endif
    internal class AzureSignalRStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> build)
        {
            return app =>
            {
                // We need to call this today because it throws if we didn't call this and called AddAzureSignalR
                // That needs to be changed.
                // app.UseAzureSignalR(r => { });

                build(app);

                // This can't be a hosted service because it needs to run after startup
                var service = app.ApplicationServices.GetRequiredService<AzureSignalRHostedService>();
                service.Start();

                var handler = new NegotiateHandler2(app.ApplicationServices);
#if NETCOREAPP3_0
                // Plug logic in that will look at the current endpoint and hijack the negotiate request
                app.Use(async (context, next) =>
                {
                    var hasHubMetadata = context.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>();

                    if (hasHubMetadata == null || !context.Request.Path.Value.EndsWith("/negotiate"))
                    {
                        await next();
                        return;
                    }

                    NegotiationResponse negotiateResponse = null;
                    try
                    {
                        negotiateResponse = handler.Process(context, hasHubMetadata.HubType.Name);
                    }
                    catch (AzureSignalRAccessTokenTooLongException ex)
                    {
                        // Log.NegotiateFailed(_logger, ex.Message);
                        context.Response.StatusCode = 413;
                        await context.Response.WriteAsync(ex.Message);
                        return;
                    }

                    var writer = new MemoryBufferWriter();
                    try
                    {
                        context.Response.ContentType = "application/json";
                        NegotiateProtocol.WriteResponse(negotiateResponse, writer);
                        // Write it out to the response with the right content length
                        context.Response.ContentLength = writer.Length;
                        await writer.CopyToAsync(context.Response.Body);
                    }
                    finally
                    {
                        writer.Reset();
                    }
                });
#endif
            };
        }
    }
}
