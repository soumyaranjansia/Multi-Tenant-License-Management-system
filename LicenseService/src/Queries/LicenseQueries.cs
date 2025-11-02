using MediatR;
using Gov2Biz.LicenseService.Models.DTOs;

namespace Gov2Biz.LicenseService.Queries;

/// <summary>
/// Query to get a license by ID.
/// </summary>
public record GetLicenseByIdQuery : IRequest<LicenseDetailsResponse?>
{
    public int LicenseId { get; init; }
    public string TenantId { get; init; } = string.Empty;
}

/// <summary>
/// Query to get all licenses for a tenant.
/// Role-based filtering: Admins see all, Users see only their own.
/// </summary>
public record GetLicensesByTenantQuery : IRequest<List<LicenseDetailsResponse>>
{
    public string TenantId { get; init; } = string.Empty;
    public string? Status { get; init; }
    public int? ExpiringInDays { get; init; }
    public string? UserEmail { get; init; }
    public string? UserRole { get; init; }
}
