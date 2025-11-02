using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Gov2Biz.LicenseService.Commands;
using Gov2Biz.LicenseService.Data;
using Gov2Biz.LicenseService.Models.DTOs;

namespace Gov2Biz.LicenseService.Handlers;

/// <summary>
/// Handler for RenewLicenseCommand.
/// Calls sp_RenewLicense stored procedure.
/// </summary>
public class RenewLicenseHandler : IRequestHandler<RenewLicenseCommand, RenewLicenseResponse>
{
    private readonly LicenseDbContext _context;
    private readonly ILogger<RenewLicenseHandler> _logger;

    public RenewLicenseHandler(
        LicenseDbContext context,
        ILogger<RenewLicenseHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RenewLicenseResponse> Handle(
        RenewLicenseCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Renewing license {LicenseId} for tenant {TenantId}",
            request.LicenseId,
            request.TenantId);

        // Prepare parameters
        var licenseIdParam = new SqlParameter("@LicenseId", request.LicenseId);
        var renewalDateParam = new SqlParameter("@RenewalDate", request.RenewalDate);
        var paymentRefParam = new SqlParameter("@PaymentReference", request.PaymentReference);
        var tenantIdParam = new SqlParameter("@TenantId", request.TenantId);
        var performedByParam = new SqlParameter("@PerformedBy", 
            request.PerformedBy ?? (object)DBNull.Value);

        // Call stored procedure
        var result = await _context.Database
            .SqlQueryRaw<RenewalResult>(
                "EXEC sp_RenewLicense @LicenseId, @RenewalDate, @PaymentReference, @TenantId, @PerformedBy",
                licenseIdParam,
                renewalDateParam,
                paymentRefParam,
                tenantIdParam,
                performedByParam)
            .ToListAsync(cancellationToken);

        var renewalResult = result.FirstOrDefault();

        if (renewalResult == null || renewalResult.Success != 1)
            throw new InvalidOperationException(
                $"Failed to renew license {request.LicenseId} for tenant {request.TenantId}");

        _logger.LogInformation(
            "License {LicenseId} renewed successfully to {NewExpiryDate}",
            request.LicenseId,
            renewalResult.NewExpiryDate);

        return new RenewLicenseResponse
        {
            Id = renewalResult.LicenseId,
            Status = "Active",
            ExpiryDate = renewalResult.NewExpiryDate,
            TenantId = request.TenantId
        };
    }

    // DTO for stored procedure result
    private class RenewalResult
    {
        public int Success { get; set; }
        public int LicenseId { get; set; }
        public DateTime NewExpiryDate { get; set; }
    }
}
