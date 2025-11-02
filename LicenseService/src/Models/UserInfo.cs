namespace Gov2Biz.LicenseService.Models;

/// <summary>
/// Simplified user information for admin selection
/// </summary>
public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
