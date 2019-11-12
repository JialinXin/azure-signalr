// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChatSample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR()
                    .AddAzureSignalR(options =>
                    {
                        options.ConnectionString = "Endpoint=https://jixinaue.service.signalr.net;AccessKey=cOKNRVvika0bEO93RytJUUyPPZu9dUl+M8mD2LK461U=;Version=1.0;";//"Endpoint=https://jixineastus2.service.signalr.net;AccessKey=EjRcwG0sQq3UNyfykUrN+assmIjSTsnfmCLE5f2U94s=;Version=1.0;";
                        options.ConnectionCount = 1;
                    });
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
            app.UseFileServer();
            app.UseAzureSignalR(routes =>
            {
                routes.MapHub<Chat>("/chat");
                routes.MapHub<BenchHub>("/bench");
            });
        }
    }
}
