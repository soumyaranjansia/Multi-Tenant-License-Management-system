using Gov2Biz.Shared.Context;
using Gov2Biz.Shared.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Gov2Biz.Shared.Extensions;

/// <summary>
/// Extension methods for configuring shared services and middleware.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Gov2Biz shared services including TenantContext.
    /// </summary>
    public static IServiceCollection AddGov2BizShared(this IServiceCollection services)
    {
        // Register TenantContext as scoped (per request)
        services.AddScoped<ITenantContext, TenantContext>();
        
        return services;
    }
}

/// <summary>
/// Extension methods for configuring middleware pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Gov2Biz standard middleware pipeline:
    /// 1. ErrorHandling (outermost - catches all exceptions)
    /// 2. RequestLogging (logs request/response)
    /// 3. TenantExtraction (validates and sets tenant context)
    /// </summary>
    public static IApplicationBuilder UseGov2BizMiddleware(this IApplicationBuilder app)
    {
        // Order is critical: ErrorHandling wraps everything
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<TenantMiddleware>();
        
        return app;
    }
}
