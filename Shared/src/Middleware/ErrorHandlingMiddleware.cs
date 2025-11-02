using Microsoft.AspNetCore.Http;
using Serilog;
using System.Net;
using System.Text.Json;

namespace Gov2Biz.Shared.Middleware;

/// <summary>
/// Global error handling middleware that catches all exceptions.
/// Returns standardized error JSON response with proper status codes.
/// Logs errors with full context including TenantId and RequestId.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var requestId = context.TraceIdentifier;
        var tenantId = context.Items["TenantId"]?.ToString() ?? "unknown";
        var path = context.Request.Path;
        var method = context.Request.Method;

        // Log the error with full context
        Log.Error(exception,
            "Unhandled exception | RequestId: {RequestId} | TenantId: {TenantId} | Method: {Method} | Path: {Path} | Message: {Message}",
            requestId,
            tenantId,
            method,
            path,
            exception.Message);

        // Determine status code and error code
        var (statusCode, errorCode, message) = MapExceptionToResponse(exception);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = new
            {
                message,
                code = errorCode,
                traceId = requestId
            }
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static (int statusCode, string errorCode, string message) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            // Validation exceptions (more specific first)
            ArgumentNullException => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "Required value was not provided"
            ),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                exception.Message
            ),
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                "INVALID_OPERATION",
                exception.Message
            ),
            
            // Authorization exceptions
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                "You are not authorized to perform this action"
            ),
            
            // Not found exceptions
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                "The requested resource was not found"
            ),
            
            // Default to internal server error
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred. Please contact support if the problem persists."
            )
        };
    }
}
