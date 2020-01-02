using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    public class ScaleOptions
    {
        /// <summary>
        /// Gets or sets the connection string of Azure SignalR Service instance.
        /// </summary>
        public ServiceEndpoint[] Endpoints { get; set; }


        /// <summary>
        /// Gets or sets the the flag to enable scale Azure SignalR Endpoints by configuration updates.
        /// </summary>
        public bool ConfigurationScale { get; set; } = true;
    }

    public class ConfigureScaleOptions : IConfigureOptions<ScaleOptions>
    {
        private readonly bool _configureScale;
        private readonly ServiceEndpoint[] _endpoints;

        public ConfigureScaleOptions(IConfiguration configuration)
        {
            _configureScale = true;
            if (bool.TryParse(configuration["ConfigureScale"], out var configureScale))
            {
                _configureScale = configureScale;
            }

            _endpoints = configuration.GetSection("Endpoints")?
                .GetChildren().Select(
                c => new ServiceEndpoint
                (
                    c.GetSection("ConnectionString").Value,
                    (EndpointType)Enum.Parse(typeof(EndpointType), c.GetSection("EndpointType").Value),
                    c.GetSection("Name").Value
                )).ToArray();
        }

        public void Configure(ScaleOptions options)
        {
            options.ConfigurationScale = _configureScale;
            options.Endpoints = _endpoints;
        }
    }
}
