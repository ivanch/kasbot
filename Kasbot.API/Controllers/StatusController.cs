using Kasbot.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kasbot.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : Controller
    {
        private readonly StatusService statusService;

        public StatusController(StatusService statusService)
        {
            this.statusService = statusService;
        }

        [HttpGet("ok")]
        public async Task<IActionResult> IsOk()
        {
            var result = await statusService.IsOk();
            return Ok(result);
        }
    }
}
