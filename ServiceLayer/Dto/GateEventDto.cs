using System;

namespace GatemetricsDashboard.ServiceLayer.Dto
{
    public class GateEventDto
    {
        public string Gate { get; init; } = null!;
        public DateTime Timestamp { get; init; } // UTC
        public int NumberOfPeople { get; init; }
        public string Type { get; init; } = null!;
        public int? Id { get; init; }
    }
}