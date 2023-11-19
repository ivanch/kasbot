using Microsoft.AspNetCore.Mvc;

namespace Kasbot.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReadinessController : ControllerBase
    {
        private readonly ILogger<ReadinessController> logger;

        public ReadinessController(ILogger<ReadinessController> logger)
        {
            this.logger = logger;
        }

        [HttpGet(Name = "readiness")]
        public IActionResult GetReadiness()
        {
            return Ok();
        }
    }
}