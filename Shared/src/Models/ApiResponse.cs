namespace Gov2Biz.Shared.Models;

/// <summary>
/// Base API response model for standardized responses.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ErrorDetails? Error { get; set; }
    
    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };
    
    public static ApiResponse<T> Fail(string message, string code) => new()
    {
        Success = false,
        Error = new ErrorDetails { Message = message, Code = code }
    };
}

/// <summary>
/// Error details for API responses.
/// </summary>
public class ErrorDetails
{
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? TraceId { get; set; }
}

/// <summary>
/// Standard error response matching middleware contract.
/// </summary>
public class ErrorResponse
{
    public ErrorDetails Error { get; set; } = new();
}
