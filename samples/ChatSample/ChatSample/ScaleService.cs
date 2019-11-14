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
            
            _serviceScaleManager.AddServiceEndpoint(new ServiceEndpoint("Endpoint=https://jixinuk.service.signalr.net;AccessKey=GAh6PWaM9AD/Z34zgqYjdaj78nurzD6cV+gSi98WMA8=;Version=1.0;"));
            return Ok();
        }
    }
}
