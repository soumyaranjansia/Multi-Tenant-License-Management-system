namespace Gov2Biz.LicenseService.Models;

/// <summary>
/// Tenant entity for multi-tenant support.
/// </summary>
public class Tenant
{
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// License entity matching database schema.
/// </summary>
public class License
{
    public int Id { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime ExpiryDate { get; set; }
    public decimal Amount { get; set; }
    public string? Metadata { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation property (not mapped - populated from JSON)
    public string? History { get; set; }
}

/// <summary>
/// License history entry.
/// </summary>
public class LicenseHistory
{
    public int Id { get; set; }
    public int LicenseId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// User entity for authentication.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty; // CSV
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
