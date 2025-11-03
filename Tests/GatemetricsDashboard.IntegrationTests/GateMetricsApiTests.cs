using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;
using GatemetricsDashboard.RepositoryLayer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GatemetricsDashboard.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // inject a test Jwt configuration and use InMemory EF for tests
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                // Use nullable value type for dictionary values to match AddInMemoryCollection signature
                conf.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "test-key-please-change",
                    ["Jwt:Issuer"] = "Gatemetrics",
                    ["Jwt:Audience"] = "GatemetricsClient"
                });
            });

            builder.ConfigureServices(static services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<GateMetricsDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                // Register InMemory DbContext for tests
                services.AddDbContext<GateMetricsDbContext>(static options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });

                // Ensure DB is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GateMetricsDbContext>();
                db.Database.EnsureCreated();
            });
        }
    }

    public class GateMetricsApiTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly string _jwtKey = "test-key-please-change";
        private readonly string _issuer = "Gatemetrics";
        private readonly string _audience = "GatemetricsClient";

        public GateMetricsApiTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private string GenerateToken()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: new[] { new Claim(ClaimTypes.Name, "testuser") },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<HttpResponseMessage> PostGateEventAsync(HttpClient client, string token, object payload)
        {
            client.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return await client.PostAsync("/GateMetrics/gate-event", content);
        }

        private static int GetIntFromElement(JsonElement el, string propName)
        {
            if (el.TryGetProperty(propName, out var p) && p.ValueKind == JsonValueKind.Number)
                return p.GetInt32();

            // try camelCase fallback
            var camel = char.ToLowerInvariant(propName[0]) + propName.Substring(1);
            if (el.TryGetProperty(camel, out p) && p.ValueKind == JsonValueKind.Number)
                return p.GetInt32();

            throw new KeyNotFoundException($"Property {propName} not found as number in element.");
        }

        private static string GetStringFromElement(JsonElement el, string propName)
        {
            if (el.TryGetProperty(propName, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString()!;

            var camel = char.ToLowerInvariant(propName[0]) + propName.Substring(1);
            if (el.TryGetProperty(camel, out p) && p.ValueKind == JsonValueKind.String)
                return p.GetString()!;

            throw new KeyNotFoundException($"Property {propName} not found as string in element.");
        }

        [Fact]
        public async Task PostGateEvent_WithoutToken_ReturnsUnauthorized()
        {
            using var client = _factory.CreateClient();
            var payload = new
            {
                Gate = "Gate A",
                Timestamp = DateTime.UtcNow,
                NumberOfPeople = 3,
                Type = "enter"
            };

            var resp = await PostGateEventAsync(client, token: string.Empty, payload);

            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task PostGateEvent_WithToken_PersistsEvent_ReturnsOk()
        {
            using var client = _factory.CreateClient();
            var token = GenerateToken();

            var now = DateTime.UtcNow;
            var payload = new
            {
                Gate = "Gate B",
                Timestamp = now,
                NumberOfPeople = 7,
                Type = "enter"
            };

            var resp = await PostGateEventAsync(client, token, payload);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            // verify persisted in InMemory DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GateMetricsDbContext>();
            var evt = await db.GateAccessEvents.OrderByDescending(e => e.Id).FirstOrDefaultAsync();
            evt.Should().NotBeNull();
            evt!.Gate.Should().Be("Gate B");
            evt.NumberOfPeople.Should().Be(7);
            evt.Type.Should().Be("enter");
            // timestamp stored as UTC close to now
            evt.Timestamp.Offset.Should().Be(TimeSpan.Zero);
            (evt.Timestamp - now).Duration().Should().BeLessThan(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task GetSummary_WithToken_ReturnsAggregatedResults()
        {
            using var client = _factory.CreateClient();
            var token = GenerateToken();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // arrange - create two events for same gate/type that should be aggregated
            var baseTimestamp = DateTime.UtcNow;
            var e1 = new { Gate = "Gate C", Timestamp = baseTimestamp, NumberOfPeople = 4, Type = "enter" };
            var e2 = new { Gate = "Gate C", Timestamp = baseTimestamp.AddSeconds(10), NumberOfPeople = 6, Type = "enter" };
            await PostGateEventAsync(client, token, e1);
            await PostGateEventAsync(client, token, e2);

            // query summary over a window that includes the events
            var start = baseTimestamp.AddMinutes(-1).ToString("o");
            var end = baseTimestamp.AddMinutes(1).ToString("o");
            var url = $"/GateMetrics/summary?gate={Uri.EscapeDataString("Gate C")}&start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}";

            var resp = await client.GetAsync(url);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

            // find the aggregated entry for Gate C / enter
            var arr = doc.RootElement.EnumerateArray().ToArray();
            arr.Should().NotBeEmpty();

            var match = arr.FirstOrDefault(el =>
            {
                try
                {
                    return GetStringFromElement(el, "Gate") == "Gate C" && GetStringFromElement(el, "Type") == "enter";
                }
                catch { return false; }
            });

            match.ValueKind.Should().Be(JsonValueKind.Object);
            var total = GetIntFromElement(match, "NumberOfPeople");
            total.Should().Be(10); // 4 + 6
        }

        [Fact]
        public async Task LiveEndpoint_ReturnsRecentEvents()
        {
            using var client = _factory.CreateClient();
            var token = GenerateToken();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var now = DateTime.UtcNow;
            var payload = new { Gate = "Gate D", Timestamp = now, NumberOfPeople = 2, Type = "leave" };
            await PostGateEventAsync(client, token, payload);

            var url = $"/GateMetrics/live?minutes=1&gate={Uri.EscapeDataString("Gate D")}";
            var resp = await client.GetAsync(url);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
            var arr = doc.RootElement.EnumerateArray().ToArray();
            arr.Should().NotBeEmpty();

            var match = arr.FirstOrDefault(el =>
            {
                try
                {
                    return GetStringFromElement(el, "Gate") == "Gate D" && GetStringFromElement(el, "Type") == "leave";
                }
                catch { return false; }
            });

            match.ValueKind.Should().Be(JsonValueKind.Object);
            GetIntFromElement(match, "NumberOfPeople").Should().Be(2);
        }

        [Fact]
        public async Task SummaryEndpoint_WithoutToken_ReturnsUnauthorized()
        {
            using var client = _factory.CreateClient();

            var start = DateTime.UtcNow.AddDays(-1).ToString("o");
            var end = DateTime.UtcNow.AddDays(1).ToString("o");
            var url = $"/GateMetrics/summary?gate={Uri.EscapeDataString("Gate A")}&start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}";

            var resp = await client.GetAsync(url);
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}