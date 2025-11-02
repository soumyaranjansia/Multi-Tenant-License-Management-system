using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

namespace Gov2Biz.Shared.Middleware;

/// <summary>
/// Middleware that logs request start and end with timing information.
/// Captures RequestId, TenantId, Method, Path, StatusCode, and Duration.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxBodyLogSize = 2048; // 2KB

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or use existing RequestId
        var requestId = context.TraceIdentifier;
        
        if (string.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString();
            context.TraceIdentifier = requestId;
        }

        // Extract TenantId from context items (set by TenantMiddleware)
        var tenantId = context.Items["TenantId"]?.ToString() ?? "unknown";

        // Start timing
        var stopwatch = Stopwatch.StartNew();

        // Log request start
        Log.Information(
            "RequestStart: {Method} {Path}{QueryString} | RequestId: {RequestId} | TenantId: {TenantId}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            requestId,
            tenantId);

        // Optionally log request body for non-GET requests (truncate if too large)
        if (context.Request.Method != HttpMethods.Get && 
            context.Request.ContentLength.HasValue && 
            context.Request.ContentLength.Value > 0 &&
            context.Request.ContentLength.Value < MaxBodyLogSize)
        {
            context.Request.EnableBuffering();
            var requestBody = await ReadRequestBody(context.Request);
            if (!string.IsNullOrEmpty(requestBody))
            {
                Log.Debug("RequestBody: {RequestBody}", requestBody);
            }
            context.Request.Body.Position = 0;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log request end
            Log.Information(
                "RequestEnd: {Method} {Path} | RequestId: {RequestId} | TenantId: {TenantId} | StatusCode: {StatusCode} | DurationMs: {DurationMs}",
                context.Request.Method,
                context.Request.Path,
                requestId,
                tenantId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            return body.Length > MaxBodyLogSize 
                ? body.Substring(0, MaxBodyLogSize) + "... [truncated]" 
                : body;
        }
        catch
        {
            return string.Empty;
        }
    }
}
