using GatemetricsDashboard.ServiceLayer.GateOperations;
using GatemetricsDashboard.ServiceLayer.Interface;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GatemetricsDashboard.ApiLayer.Controllers
{
    //// [Authorize]
    //[ApiController]
    //[Route("[controller]")]
    //public class GateMetricsController : ControllerBase
    //{
    //    private readonly IMediator _mediator;

    //    public GateMetricsController(IMediator mediator)
    //    {
    //        _mediator = mediator;
    //    }

    //    // POST /GateMetrics/gate-event
    //    [HttpPost("gate-event")]
    //    public async Task<IActionResult> CreateGateEvent([FromBody] CreateGateEventCommand command)
    //    {
    //        var result = await _mediator.Send(command);
    //        return result ? Ok() : BadRequest();
    //    }

    //    // GET /GateMetrics/summary?gate=...&type=...&start=...&end=...
    //    [HttpGet("summary")]
    //    public async Task<IActionResult> GetSummary([FromQuery] string? gate, [FromQuery] string? type, [FromQuery] DateTime? start, [FromQuery] DateTime? end)
    //    {
    //        var query = new GateEventSummaryQuery(gate, type, start, end);
    //        var result = await _mediator.Send(query);
    //        return Ok(result);
    //    }

    //    // GET /GateMetrics/live?minutes=1&gate=...&type=...
    //    [HttpGet("live")]
    //    public async Task<IActionResult> GetLive([FromQuery] int minutes = 1, [FromQuery] string? gate = null, [FromQuery] string? type = null)
    //    {
    //        if (minutes <= 0) minutes = 1;
    //        var end = DateTime.UtcNow;
    //        var start = end.AddMinutes(-minutes);
    //        var query = new GateEventSummaryQuery(gate, type, start, end);
    //        var result = await _mediator.Send(query);
    //        return Ok(result);
    //    }
    //}

    namespace GatemetricsDashboard.ApiLayer.Controllers
    {
        // [Authorize]
        [ApiController]
        [Route("[controller]")]
        public class GateMetricsController : ControllerBase
        {
            private readonly IGateMetricsService _gateMetricsService;

            public GateMetricsController(IGateMetricsService gateMetricsService)
            {
                _gateMetricsService = gateMetricsService;
            }

            // POST /GateMetrics/gate-event
            [HttpPost("gate-event")]
            public async Task<IActionResult> CreateGateEvent([FromBody] CreateGateEventCommand command)
            {
                var result = await _gateMetricsService.CreateGateEvent(command);
                return result ? Ok() : BadRequest();
            }

            // GET /GateMetrics/summary?gate=...&type=...&start=...&end=...
            [HttpGet("summary")]
            public async Task<IActionResult> GetSummary([FromQuery] string? gate, [FromQuery] string? type, [FromQuery] DateTime? start, [FromQuery] DateTime? end)
            {
                var result = await _gateMetricsService.GetSummary(gate, type, start, end);
                return Ok(result);
            }

            // GET /GateMetrics/live?minutes=1&gate=...&type=...
            [HttpGet("live")]
            public async Task<IActionResult> GetLive([FromQuery] int minutes = 1, [FromQuery] string? gate = null, [FromQuery] string? type = null)
            {
                var result = await _gateMetricsService.GetLive(minutes, gate, type);
                return Ok(result);
            }
        }
    }
}