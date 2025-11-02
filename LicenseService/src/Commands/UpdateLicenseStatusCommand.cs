using MediatR;

namespace Gov2Biz.LicenseService.Commands;

/// <summary>
/// Command to update license status (approval/rejection/suspension).
/// </summary>
public class UpdateLicenseStatusCommand : IRequest<bool>
{
    public int LicenseId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string PerformedBy { get; init; } = string.Empty;
}
