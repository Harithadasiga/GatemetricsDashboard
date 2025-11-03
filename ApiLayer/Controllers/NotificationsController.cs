using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GatemetricsDashboard.ServiceLayer.Notifications;

namespace GatemetricsDashboard.ApiLayer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly WebhookService _webhookService;

        public NotificationsController(WebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        // POST /Notifications/webhooks { "url": "https://example.com/hook" }
        [HttpPost("webhooks")]
        public IActionResult Register([FromBody] WebhookRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
                return BadRequest();

            var ok = _webhookService.Register(request.Url);
            return ok ? Created(string.Empty, null) : Conflict();
        }

        // DELETE /Notifications/webhooks?url=https://example.com/hook
        [HttpDelete("webhooks")]
        public IActionResult Unregister([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest();

            var ok = _webhookService.Unregister(url);
            return ok ? NoContent() : NotFound();
        }

        // GET /Notifications/webhooks
        [HttpGet("webhooks")]
        public IActionResult GetAll() => Ok(_webhookService.GetAll());

        public record WebhookRequest(string Url);
    }
}