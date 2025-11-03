using GatemetricsDashboard.ServiceLayer.Dto;
using MediatR;

namespace GatemetricsDashboard.ServiceLayer.GateOperations
{
    /// <summary>
    /// Query to retrieve aggregated sensor event data.
    /// </summary>
    public record GateEventSummaryQuery(
        string? Gate,
        string? Type,
        DateTime? Start,
        DateTime? End
    ) : IRequest<List<GateSummaryDto>>;
}
