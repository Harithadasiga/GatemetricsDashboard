using System;

namespace GatemetricsDashboard.DomainLayer
{
    public class GateAccessEvent
    {
        public int Id { get; set; }
        public string Gate { get; set; } = string.Empty;
        // changed from DateTime to DateTimeOffset to preserve original timestamp + offset
        public DateTimeOffset Timestamp { get; set; }
        public int NumberOfPeople { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
