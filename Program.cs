using GatemetricsDashboard.RepositoryLayer;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MediatR;
using GatemetricsDashboard.ServiceLayer.GateOperations;
using GatemetricsDashboard.ApiLayer.Hubs;
using GatemetricsDashboard.ServiceLayer.Notifications;
using Microsoft.OpenApi.Models;
using GatemetricsDashboard.ServiceLayer.Interface; // for IGateMetricsService
using GatemetricsDashboard.ApiLayer.Services; // for GateMetricsService
using GateMetrics.Services; // for GateSensorEventService
using GatemetricsData.ServiceLayer.Auth; // JwtOptions, JwtTokenService
using GatemetricsData.ServiceLayer.Interface; // ITokenService

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        // Configure Swagger to show the Authorize button for Bearer tokens
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "GateMetrics API", Version = "v1" });

            // Add Bearer auth to Swagger UI
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter JWT as: Bearer {your token}"
            };
            c.AddSecurityDefinition("Bearer", securityScheme);

            var securityRequirement = new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, new string[] { } }
            };
            c.AddSecurityRequirement(securityRequirement);
        });

        // Bind JwtOptions for token generation and validation and register token service
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
        builder.Services.AddSingleton<ITokenService, JwtTokenService>();

        var jwtKey = builder.Configuration["Jwt:Key"];
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];

        // Configure authentication/authorization (kept functionally identical)
        ConfigureJwtIfConfigured(builder, jwtKey, jwtIssuer, jwtAudience);

        // --- CORS: allow the dashboard / front-end origins to access SignalR ---
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowDashboard", policy =>
            {
                policy.WithOrigins("https://localhost:7054", "http://localhost:5200") // update to match the client origin(s)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials(); // required for SignalR when using WebSockets or cookies
            });
        });
        // -----------------------------------------------------------------------

         // DbContext
        builder.Services.AddDbContext<GateMetricsDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? "")
                   .LogTo(Console.WriteLine, LogLevel.Information)
                   .EnableSensitiveDataLogging()
        );

        // MediatR
        builder.Services.AddMediatR(typeof(CreateGateEventHandler).Assembly);

        // SignalR
        builder.Services.AddSignalR();

        // Http client + webhook service
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<WebhookService>();

        // Register application services
        builder.Services.AddScoped<IGateMetricsService, GateMetricsService>();
        // Register background hosted service that generates synthetic gate events
        builder.Services.AddHostedService<GateSensorEventService>();

        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(60),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));
            options.RejectionStatusCode = 429;
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // Apply CORS policy BEFORE mapping the hub/endpoints.
        app.UseCors("AllowDashboard");

        // Ensure auth middleware runs if JWT configured
        var hasJwt = !string.IsNullOrEmpty(jwtKey) && !string.IsNullOrEmpty(jwtIssuer) && !string.IsNullOrEmpty(jwtAudience);
        if (hasJwt)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.UseRateLimiter();
        app.MapControllers();

        // SignalR endpoint
        app.MapHub<GateEventsHub>("/hubs/gateevents");

        app.Run();

        static void ConfigureJwtIfConfigured(WebApplicationBuilder builder, string? key, string? issuer, string? audience)
        {
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(issuer) && !string.IsNullOrEmpty(audience))
            {
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = true;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateLifetime = true
                    };

                    // Allow tokens to be passed via query string for SignalR (hub) connections
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/gateevents"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context => Task.CompletedTask,
                        OnAuthenticationFailed = context => Task.CompletedTask
                    };
                });

                builder.Services.AddAuthorization();
            }
            else
            {
                // If JWT not configured, register a no-op authorization so code depending on it still resolves.
                builder.Services.AddAuthorization();
                builder.Logging?.AddConsole();
                Console.WriteLine("Warning: JWT configuration missing. Authentication is disabled. Set Jwt:Key, Jwt:Issuer and Jwt:Audience in configuration to enable authentication.");
            }
        }
    }
}
