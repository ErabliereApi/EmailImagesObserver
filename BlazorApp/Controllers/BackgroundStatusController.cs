using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BlazorApp.AzureComputerVision;

namespace BlazorApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [EnableUsecureController]
    public class BackgroundTaskStatusController : ControllerBase
    {
        private readonly IdleClient _idleClient;

        public BackgroundTaskStatusController(IdleClient idleClient)
        {
            _idleClient = idleClient;
        }

        [HttpGet]
        public ActionResult<string> GetBackgroundTaskStatus()
        {
            return Ok(_idleClient.GetBackgroundTaskStatus());
        }
    }
}