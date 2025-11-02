using MediatR;
using Gov2Biz.LicenseService.Models.DTOs;

namespace Gov2Biz.LicenseService.Commands;

/// <summary>
/// Command to create a new license.
/// </summary>
public record CreateLicenseCommand : IRequest<CreateLicenseResponse>
{
    public string ApplicantName { get; init; } = string.Empty;
    public string ApplicantEmail { get; init; } = string.Empty;
    public string LicenseType { get; init; } = string.Empty;
    public decimal Amount { get; init; } = 100.00m;
    public DateTime ExpiryDate { get; init; }
    public string? Metadata { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string PerformedBy { get; init; } = string.Empty;
    public bool IsAdminCreated { get; init; } = false;
}

/// <summary>
/// Command to renew an existing license.
/// </summary>
public record RenewLicenseCommand : IRequest<RenewLicenseResponse>
{
    public int LicenseId { get; init; }
    public DateTime RenewalDate { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string PerformedBy { get; init; } = string.Empty;
}
