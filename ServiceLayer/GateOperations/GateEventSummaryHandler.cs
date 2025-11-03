using GatemetricsDashboard.ServiceLayer.Dto;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.Extensions.Logging;
using GatemetricsDashboard.DomainLayer;
using GatemetricsDashboard.RepositoryLayer;
using GatemetricsDashboard.ServiceLayer.Notifications;

namespace GatemetricsDashboard.ServiceLayer.GateOperations
{
    /// <summary>
    /// Handles create (command) and summary (query) operations for gate events.
    /// </summary>

    // Handler responsible for persisting incoming gate events
    public class CreateGateEventHandler : IRequestHandler<CreateGateEventCommand, bool>
    {
        private readonly GateMetricsDbContext _context;
        private readonly ILogger<CreateGateEventHandler> _logger;

        public CreateGateEventHandler(GateMetricsDbContext context, ILogger<CreateGateEventHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> Handle(CreateGateEventCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Saving gate event: Gate={Gate} Type={Type} Timestamp={Timestamp} Count={Count}",
                request.Gate, request.Type, request.Timestamp, request.NumberOfPeople);

            var entity = new GateAccessEvent
            {
                Gate = request.Gate,
                // store timestamp as UTC DateTime
                Timestamp = request.Timestamp.UtcDateTime,
                NumberOfPeople = request.NumberOfPeople,
                Type = request.Type
            };

            _context.GateAccessEvents.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Gate event saved with Id={Id}", entity.Id);
            return true;
        }
    }

    // Handler responsible for aggregating stored gate events
    public class GateEventSummaryQueryHandler : IRequestHandler<GateEventSummaryQuery, List<GateSummaryDto>>
    {
        private readonly GateMetricsDbContext _context;
        private readonly ILogger<GateEventSummaryQueryHandler> _logger;

        public GateEventSummaryQueryHandler(GateMetricsDbContext context, ILogger<GateEventSummaryQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<GateSummaryDto>> Handle(GateEventSummaryQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GateEventSummaryQuery: Gate={Gate}, Type={Type}, Start={Start}, End={End}",
                request.Gate, request.Type, request.Start, request.End);

            var query = _context.GateAccessEvents.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Gate))
                query = query.Where(e => e.Gate == request.Gate);

            if (!string.IsNullOrWhiteSpace(request.Type))
                query = query.Where(e => e.Type == request.Type);

            DateTime? startUtc = null;
            DateTime? endUtc = null;

            if (request.Start.HasValue)
            {
                // ensure comparison against UTC if stored as UTC
                startUtc = DateTime.SpecifyKind(request.Start.Value, DateTimeKind.Utc);
                query = query.Where(e => e.Timestamp >= startUtc.Value);
            }

            if (request.End.HasValue)
            {
                endUtc = DateTime.SpecifyKind(request.End.Value, DateTimeKind.Utc);
                query = query.Where(e => e.Timestamp <= endUtc.Value);
            }

            // Diagnostic logging: show time window and generated SQL + parameters
            try
            {
                _logger.LogInformation("Final query time window (UTC): Start={StartUtc}, End={EndUtc}", startUtc, endUtc);

                // ToQueryString() gives the SQL EF Core will execute (useful for debugging)
                var sql = query.ToQueryString();
                _logger.LogInformation("EF generated SQL: {Sql}", sql);
            }
            catch (Exception ex)
            {
                // ToQueryString might throw in some scenarios; log and continue
                _logger.LogWarning(ex, "Failed to generate query SQL with ToQueryString()");
            }

            var result = await query
                .GroupBy(e => new { e.Gate, e.Type })
                .Select(g => new GateSummaryDto
                {
                    Gate = g.Key.Gate,
                    Type = g.Key.Type,
                    NumberOfPeople = g.Sum(e => e.NumberOfPeople)
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Aggregated {Count} gate summaries.", result.Count);
            return result;
        }
    }
}

