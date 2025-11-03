using MediatR;

namespace GatemetricsDashboard.ServiceLayer.GateOperations
{
    // Command for creating (saving) a single gate event (JSON -> DB)
    public record CreateGateEventCommand(
        string Gate,
        DateTimeOffset Timestamp,
        int NumberOfPeople,
        string Type
    ) : IRequest<bool>;
}
