// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace ChatSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .ConfigureLogging(
                (hostingContext, logging) =>
                {
                    logging.AddTimedConsole();
                    logging.AddDebug();
                })
            .ConfigureAppConfiguration(ConfigurationConfig)
            .UseStartup<Startup>();

        public static readonly Action<WebHostBuilderContext, IConfigurationBuilder> ConfigurationConfig =
            (context, builder) =>
            {
                builder
                    .SetBasePath(context.HostingEnvironment.ContentRootPath)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddJsonFile($"endpoints.json", optional: true, reloadOnChange: true);
            };

    }
}
