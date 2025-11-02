using Gov2Biz.LicenseService.Data;
using Gov2Biz.LicenseService.Jobs;
using Gov2Biz.LicenseService.Services;
using Gov2Biz.Shared.Context;
using Gov2Biz.Shared.Extensions;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "LicenseService")
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        path: "/Logs/licenseservice-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Gov2Biz LicenseService");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Add Swagger with JWT authentication
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Gov2Biz License Service API",
            Version = "v1",
            Description = "Multi-tenant license management service with CQRS pattern"
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Database configuration
    builder.Services.AddDbContext<LicenseDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Add Gov2Biz Shared services (TenantContext)
    builder.Services.AddGov2BizShared();

    // Add MediatR
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // Add application services
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IAuthService, AuthService>();    // JWT Authentication - Use same key as JwtService for consistency
    var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["JWT:Secret"] 
        ?? throw new InvalidOperationException("JWT key not configured (Jwt:Key or JWT:Secret required)");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["JWT:Issuer"] ?? "Gov2Biz";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["JWT:Audience"] ?? "Gov2Biz";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
            };
            
            // Log JWT validation failures for debugging
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Error("JWT Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Log.Debug("JWT token validated successfully for user: {User}", 
                        context.Principal?.Identity?.Name ?? "Unknown");
                    return Task.CompletedTask;
                }
            };
        });builder.Services.AddAuthorization();

    // Hangfire with Redis - with retry logic
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    
    // Wait for Redis to be fully ready
    var maxRetries = 10;
    var retryCount = 0;
    ConnectionMultiplexer? redis = null;
    
    while (retryCount < maxRetries)
    {
        try
        {
            redis = ConnectionMultiplexer.Connect(redisConnectionString);
            if (redis.IsConnected)
            {
                Log.Information("Successfully connected to Redis");
                break;
            }
        }
        catch (Exception ex)
        {
            retryCount++;
            Log.Warning(ex, "Failed to connect to Redis (attempt {RetryCount}/{MaxRetries})", retryCount, maxRetries);
            if (retryCount >= maxRetries)
            {
                throw;
            }
            Thread.Sleep(2000); // Wait 2 seconds before retry
        }
    }
    
    builder.Services.AddHangfire(config =>
    {
        config.UseRedisStorage(redis, new RedisStorageOptions
        {
            Prefix = "gov2biz:licenseservice:",
            ExpiryCheckInterval = TimeSpan.FromHours(1)
        });
    });

    builder.Services.AddHangfireServer();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LicenseService API v1"));
    }

    // Use Gov2Biz global middlewares (ErrorHandling, RequestLogging, Tenant)
    app.UseGov2BizMiddleware();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Hangfire Dashboard (optional, for monitoring)
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });    // Register Hangfire recurring jobs
    RecurringJob.AddOrUpdate<LicenseRenewalJob>(
        "check-expiring-licenses",
        job => job.ExecuteAsync(),
        Cron.Daily(2)); // Run daily at 2 AM UTC

    Log.Information("LicenseService started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LicenseService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Simple Hangfire authorization filter (allow all in dev, restrict in production)
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In production, implement proper authorization
        return true;
    }
}
