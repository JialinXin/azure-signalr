using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: HostingStartup(typeof(AzureSignalRHostingStartup))]

namespace Microsoft.Azure.SignalR.Startup
{
    public class AzureSignalRHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                if (!context.HostingEnvironment.IsDevelopment() || context.Configuration.GetSection("Azure:SignalR:Enabled").Get<bool>())
                {
                    services.AddSignalR().AddAzureSignalR();
                }
            });
        }
    }
}
