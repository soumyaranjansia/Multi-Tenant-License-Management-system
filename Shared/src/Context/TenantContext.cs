using System.Text.RegularExpressions;

namespace Gov2Biz.Shared.Context;

/// <summary>
/// Scoped service that provides tenant context for the current request.
/// Set by TenantMiddleware and available for DI throughout the request lifecycle.
/// </summary>
public interface ITenantContext
{
    string? TenantId { get; }
    void SetTenantId(string tenantId);
}

public class TenantContext : ITenantContext
{
    private string? _tenantId;
    
    // Regex for validating tenant IDs: alphanumeric with dashes and underscores, 1-50 chars
    private static readonly Regex TenantIdRegex = new(@"^[A-Za-z0-9\-_]{1,50}$", RegexOptions.Compiled);

    public string? TenantId => _tenantId;

    public void SetTenantId(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId cannot be null or empty.", nameof(tenantId));
            
        if (!TenantIdRegex.IsMatch(tenantId))
            throw new ArgumentException(
                "TenantId must be alphanumeric with dashes or underscores only, max 50 characters.", 
                nameof(tenantId));
        
        _tenantId = tenantId;
    }
    
    public static bool IsValidTenantId(string? tenantId)
    {
        return !string.IsNullOrWhiteSpace(tenantId) && TenantIdRegex.IsMatch(tenantId);
    }
}
