namespace Gov2Biz.LicenseService.Models.DTOs;

/// <summary>
/// Request to create a new license.
/// </summary>
public record CreateLicenseRequest
{
    public string ApplicantName { get; init; } = string.Empty;
    public string ApplicantEmail { get; init; } = string.Empty;
    public string LicenseType { get; init; } = string.Empty;
    public decimal Amount { get; init; } = 100.00m; // Default license fee
    public DateTime ExpiryDate { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Response after creating a license.
/// </summary>
public record CreateLicenseResponse
{
    public int Id { get; init; }
    public string LicenseNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ExpiryDate { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Response for license details with history.
/// </summary>
public record LicenseDetailsResponse
{
    public int Id { get; init; }
    public string LicenseNumber { get; init; } = string.Empty;
    public string ApplicantName { get; init; } = string.Empty;
    public string ApplicantEmail { get; init; } = string.Empty;
    public string LicenseType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ExpiryDate { get; init; }
    public decimal Amount { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<HistoryEntry>? History { get; init; }
}

/// <summary>
/// License history entry.
/// </summary>
public record HistoryEntry
{
    public DateTime Timestamp { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? PerformedBy { get; init; }
}

/// <summary>
/// Request to renew a license.
/// </summary>
public record RenewLicenseRequest
{
    public DateTime RenewalDate { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
}

/// <summary>
/// Response after renewing a license.
/// </summary>
public record RenewLicenseResponse
{
    public int Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime ExpiryDate { get; init; }
    public string TenantId { get; init; } = string.Empty;
}

/// <summary>
/// Request to update license status (Admin only).
/// </summary>
public record UpdateLicenseStatusRequest
{
    public string Status { get; init; } = string.Empty; // Active, Rejected, Suspended
    public string? Reason { get; init; }
}

/// <summary>
/// Login request.
/// </summary>
public record LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Login response with JWT token.
/// </summary>
public record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public string[] Roles { get; init; } = Array.Empty<string>();
    public string TenantId { get; init; } = string.Empty;
    public int UserId { get; init; }
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Register new user request.
/// </summary>
public record RegisterRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Role { get; init; } = "User";
}

/// <summary>
/// Register response after creating a new user.
/// </summary>
public record RegisterResponse
{
    public int UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
}
