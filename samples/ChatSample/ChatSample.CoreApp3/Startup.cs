using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChatSample.CoreApp3
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSignalR().AddAzureSignalR(options =>
            {
                options.ConnectionCount = 2;
                options.Endpoints = new Microsoft.Azure.SignalR.ServiceEndpoint[] {
                new Microsoft.Azure.SignalR.ServiceEndpoint("Endpoint=https://jixineastus2.service.signalr.net;AccessKey=EjRcwG0sQq3UNyfykUrN+assmIjSTsnfmCLE5f2U94s=;Version=1.0;"),
                new Microsoft.Azure.SignalR.ServiceEndpoint("Endpoint=https://jixinuk.service.signalr.net;AccessKey=GAh6PWaM9AD/Z34zgqYjdaj78nurzD6cV+gSi98WMA8=;Version=1.0;")};
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseFileServer();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(routes =>
            {
                routes.MapHub<Chat>("/chat");
                routes.MapHub<NotificationHub>("/notificatons");
            });
        }
    }
}
