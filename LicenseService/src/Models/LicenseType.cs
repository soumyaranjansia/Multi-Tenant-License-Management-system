namespace Gov2Biz.LicenseService.Models;

/// <summary>
/// License type with pricing information
/// </summary>
public class LicenseType
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public int DurationMonths { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
