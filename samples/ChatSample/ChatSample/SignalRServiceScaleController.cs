using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSample
{
    [Route("scale")]
    [ApiController]
    public class SignalRServiceScaleController : Controller
    {
        public IServiceScaleManager _serviceScaleManager;

        public SignalRServiceScaleController(IServiceScaleManager serviceScaleManager)
        {
            _serviceScaleManager = serviceScaleManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            _serviceScaleManager.AddServiceEndpoint(new ServiceEndpoint("Endpoint=https://jixinaue.service.signalr.net;AccessKey=cOKNRVvika0bEO93RytJUUyPPZu9dUl+M8mD2LK461U=;Version=1.0;"));
            return Ok();
        }
    }
}
