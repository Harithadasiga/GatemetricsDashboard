using GatemetricsDashboard.DomainLayer;
using Microsoft.EntityFrameworkCore;

namespace GatemetricsDashboard.RepositoryLayer
{
    public class GateMetricsDbContext : DbContext
    {
        public GateMetricsDbContext(DbContextOptions<GateMetricsDbContext> options) : base(options)
        {
        }

        public DbSet<GateAccessEvent> GateAccessEvents { get; set; } = null!;
    }
}