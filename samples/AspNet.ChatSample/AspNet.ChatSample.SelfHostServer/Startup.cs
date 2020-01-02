// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.AspNet.SignalR;
using Microsoft.Azure.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Owin;

[assembly: OwinStartup(typeof(AspNet.ChatSample.SelfHostServer.Startup))]

namespace AspNet.ChatSample.SelfHostServer
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // app.MapSignalR();
            app.UseCors(CorsOptions.AllowAll);
            app.MapAzureSignalR(GetType().FullName, options => {
                options.Endpoints = new Microsoft.Azure.SignalR.ServiceEndpoint[] {
                    new ServiceEndpoint("Endpoint=https://jixineastus2.service.signalr.net;AccessKey=EjRcwG0sQq3UNyfykUrN+assmIjSTsnfmCLE5f2U94s=;Version=1.0;"),
                    new ServiceEndpoint("Endpoint=https://jixinuk.service.signalr.net;AccessKey=GAh6PWaM9AD/Z34zgqYjdaj78nurzD6cV+gSi98WMA8=;Version=1.0;")};
            });
        }
    }
}
