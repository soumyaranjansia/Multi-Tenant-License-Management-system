using Gov2Biz.Shared.Context;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Text.Json;

namespace Gov2Biz.Shared.Middleware;

/// <summary>
/// Middleware that extracts and validates the X-Tenant-ID header.
/// Sets TenantContext and enriches logs with TenantId.
/// Returns 400 if header is missing or invalid.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private const string TenantHeaderName = "X-Tenant-ID";

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Skip tenant validation for health checks and authentication endpoints
        if (context.Request.Path.StartsWithSegments("/healthz") || 
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from header (case-insensitive)
        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdValues))
        {
            await WriteErrorResponse(context, "Missing or invalid X-Tenant-ID header", "MISSING_TENANT");
            return;
        }

        var tenantId = tenantIdValues.FirstOrDefault();
        
        // Validate tenant ID format
        if (!TenantContext.IsValidTenantId(tenantId))
        {
            await WriteErrorResponse(context, "Missing or invalid X-Tenant-ID header", "MISSING_TENANT");
            return;
        }

        // Set tenant context
        tenantContext.SetTenantId(tenantId!);
        context.Items["TenantId"] = tenantId;

        // Enrich Serilog logs with TenantId
        using (LogContext.PushProperty("TenantId", tenantId))
        {
            await _next(context);
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, string message, string code)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = new
            {
                message,
                code,
                traceId = context.TraceIdentifier
            }
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            }));
    }
}
