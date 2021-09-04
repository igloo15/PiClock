using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PiWebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PiStatusController : ControllerBase
    {
        private ILogger _logger;
        private PiStatusService _service;

        public PiStatusController(PiStatusService service, ILogger<PiStatusController> logger)
        {
            _logger = logger;
            _service = service;
        }

        [HttpGet]
        public PiStatus Get()
        {
            return _service?.Status;
        }

        [HttpGet("commands")]
        public PiCommand[] GetCommands()
        {
            return _service.GetCommands();
        }

        [HttpPost]
        public async Task UpdateClock(PiClockStatus status)
        {
            await _service.PublishClock(status);
        }
    }
}
