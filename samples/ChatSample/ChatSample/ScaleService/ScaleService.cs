using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace ChatSample
{
    public class ScaleService
    {
        private IServiceScaleManager _serviceScaleManager;
        private ServiceEndpoint[] _endpoints;

        public ScaleService(IOptionsMonitor<ScaleOptions> optionsMonitor, IServiceScaleManager serviceScaleManager)
        {
            _serviceScaleManager = serviceScaleManager;
            _endpoints = optionsMonitor.CurrentValue.AdditionalEndpoints;
            //_optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ScaleOptions>>();

            OnChange(optionsMonitor.CurrentValue);
            optionsMonitor.OnChange(OnChange);
        }


        private void OnChange(ScaleOptions options)
        {
            var endpoints = options.AdditionalEndpoints;
            var newEndpoints = endpoints.Except(_endpoints);
            foreach (var endpoint in newEndpoints)
            {
                _serviceScaleManager.AddServiceEndpoint(endpoint);
            }
        }


        
    }
}
