using System.ComponentModel.DataAnnotations;


namespace GatemetricsDashboard.ServiceLayer.Dto
{
    /// <summary>
    /// Represents the aggregated result of people flow per gate and type.
    /// </summary>
    public class GateSummaryDto
    {
        public string Gate { get; set; } = null!;
        public string Type { get; set; } = null!;
        public int NumberOfPeople { get; set; }
    }
}

