using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Formatting.Compact;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ApiGateway")
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        path: "/Logs/apigateway-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Gov2Biz API Gateway");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add Ocelot configuration file
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

    // Add Ocelot services
    builder.Services.AddOcelot();

    // Add CORS if needed
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Use CORS
    app.UseCors("AllowAll");    // Use Ocelot middleware
    await app.UseOcelot();

    Log.Information("API Gateway started successfully on port 5000");
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
