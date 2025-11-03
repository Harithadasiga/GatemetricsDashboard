# GateMetricsDashboard

This repository exposes a small API to record and summarize gate access events. The project includes JWT authentication, integration tests, and a small console Dashboard client.

Quick setup
1. Add required packages to the API project (`GatemetricsData`):
   - `dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer`
   - `dotnet add package Swashbuckle.AspNetCore` (for Swagger)

2. Required packages for the test project (`Tests/GatemetricsDashboard.IntegrationTests`):
   - `dotnet add package Microsoft.AspNetCore.Mvc.Testing --project Tests/GatemetricsDashboard.IntegrationTests`
   - `dotnet add package Microsoft.EntityFrameworkCore.InMemory --project Tests/GatemetricsDashboard.IntegrationTests`
   - `dotnet add package xunit xunit.runner.visualstudio Microsoft.NET.Test.Sdk --project Tests/GatemetricsDashboard.IntegrationTests`

3. The Dashboard console client project (`DashboardClient`) requires configuration packages (these are already added in the project file used by the sample client):
   - `Microsoft.Extensions.Configuration`
   - `Microsoft.Extensions.Configuration.FileExtensions`
   - `Microsoft.Extensions.Configuration.Json`
   - `Microsoft.Extensions.Configuration.EnvironmentVariables`
   - `Microsoft.Extensions.Configuration.CommandLine`

Database
- Update the database (if using SQL Server):
  - `dotnet ef database update` (run from the API project directory)

Configuration
- Edit `appsettings.json` in the API project (or set environment variables):
  - `ConnectionStrings:DefaultConnection` — your SQL Server connection string.
  - `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` — secret and metadata for JWT tokens. Replace the sample secret for production.

- The dashboard / client supports a token via:
  - Command-line argument: `--token <jwt>` (or `-t <jwt>`)
  - Environment variable: `ApiJwtToken`
  - `appsettings.json` key: `ApiJwtToken`

Run
- Start the API (from the API project directory):
  - `dotnet run`
- Start the console Dashboard client (from the `DashboardClient` project directory):
  - `dotnet run --project DashboardMetrics\DashboardClient.csproj -- --token <your-jwt>`
  - The client will post a sample event, request a summary, and connect to the SignalR hub at `/hubs/gateevents` (it will pass a JWT to SignalR using the access-token provider when provided).

API Endpoints
- POST `/GateMetrics/gate-event` (requires Authorization: `Bearer <token>`)
- GET `/GateMetrics/summary` (requires Authorization)
- SignalR hub: `/hubs/gateevents` (supports passing the JWT as an access token when connecting)

Testing
- Run tests (from solution root):
  - `dotnet test`

Notes
- The integration tests use an in-memory EF database and inject a test JWT configuration so tests can validate authentication behavior.
- Ensure test-only packages (for example `Microsoft.NET.Test.Sdk`) are referenced only in your test projects; they should not be added to the main web application project. The application project should not include `Microsoft.NET.Test.Sdk` to avoid test SDKs injecting extra build-time artifacts.
- For local development you can use the provided symmetric key in `appsettings.json` but rotate/use a secure secret in production.

If you need, I can also add a short `scripts/` helper to run API + Dashboard together with sample tokens for local development.


                              +----------------+
                              |  Dashboard /   |
                              |  DashboardClient|
                              |  (Console/Web) |
                              +--+----------+--+
                                 | REST / HTTP |
                                 |  (Bearer JWT)|
                                 v
                +-----------------------------------------------+
                |               GateMetrics API                 |
                |    (ASP.NET Core WebApplication / Program)    |
                |                                               |
                |  +----------------+    +-------------------+  |
                |  | Controllers    |    | SignalR Hub       |  |
                |  | (GateMetrics)  |<-->| /hubs/gateevents   |  |
                |  +----------------+    +-------------------+  |
                |         |                       ^            |
                |         v                       | publish   |
                |  +----------------+    +-------------------+  |
                |  | Service layer  |    | GateSensorEventSvc |  |
                |  | IGateMetricsSvc|    | (IHostedService)   |  |
                |  +----------------+    +-------------------+  |
                |         |                       |            |
                |         v                       | generates |
                |  +----------------+              v            |
                |  | MediatR handlers|--------> write/read DB   |
                |  +----------------+                           |
                |         |                                    |
                |         v                                    |
                |  +----------------------+                     |
                |  | GateMetricsDbContext |-------------------->|
                |  | (EF Core)            |       SQL Server     |
                |  +----------------------+                     |
                |                                               |
                |  +----------------------+                     |
                |  | WebhookService       |---> External Webhooks |
                |  +----------------------+                     |
                |                                               |
                |  +----------------------+                     |
                |  | Auth: JWT Bearer     |  validates incoming  |
                |  +----------------------+  tokens               |
                |                                               |
                |  +----------------------+                     |
                |  | Swagger + RateLimiter |                    |
                |  +----------------------+                     |
                +-----------------------------------------------+

Integration Tests (Tests project)
- Uses WebApplicationFactory<Program> to start the API in-test:
  * injects test Jwt config (in-memory) and uses InMemory EF to validate auth/logic