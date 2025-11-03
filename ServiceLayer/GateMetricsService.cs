using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using GatemetricsDashboard.ServiceLayer.GateOperations;
using GatemetricsDashboard.ServiceLayer.Interface; // ensure we implement the correct IGateMetricsService

namespace GatemetricsDashboard.ApiLayer.Services
{
    public class GateMetricsService : IGateMetricsService
    {
        private readonly IMediator _mediator;

        public GateMetricsService(IMediator mediator) => _mediator = mediator;

        public Task<bool> CreateGateEvent(CreateGateEventCommand command) => _mediator.Send(command);

        public Task<IEnumerable<object>> GetSummary(string? gate, string? type, DateTime? start, DateTime? end)
        {
            var query = new GateEventSummaryQuery(gate, type, start, end);
            return _mediator.Send(query).ContinueWith(t => (IEnumerable<object>)t.Result);
        }

        public Task<IEnumerable<object>> GetLive(int minutes, string? gate, string? type)
        {
            if (minutes <= 0) minutes = 1;
            var end = DateTime.UtcNow;
            var start = end.AddMinutes(-minutes);
            var query = new GateEventSummaryQuery(gate, type, start, end);
            return _mediator.Send(query).ContinueWith(t => (IEnumerable<object>)t.Result);
        }
    }
}