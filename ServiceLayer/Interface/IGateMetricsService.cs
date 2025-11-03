using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GatemetricsDashboard.ServiceLayer.GateOperations;

namespace GatemetricsDashboard.ServiceLayer.Interface
{
    public interface IGateMetricsService
    {
        Task<bool> CreateGateEvent(CreateGateEventCommand command);
        Task<IEnumerable<object>> GetSummary(string? gate, string? type, DateTime? start, DateTime? end);
        Task<IEnumerable<object>> GetLive(int minutes, string? gate, string? type);
    }
}