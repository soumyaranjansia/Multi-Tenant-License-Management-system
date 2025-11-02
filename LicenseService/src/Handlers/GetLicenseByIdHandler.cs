using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Gov2Biz.LicenseService.Queries;
using Gov2Biz.LicenseService.Data;
using Gov2Biz.LicenseService.Models;
using Gov2Biz.LicenseService.Models.DTOs;

namespace Gov2Biz.LicenseService.Handlers;

/// <summary>
/// Handler for GetLicenseByIdQuery.
/// Calls sp_GetLicenseById stored procedure.
/// </summary>
public class GetLicenseByIdHandler : IRequestHandler<GetLicenseByIdQuery, LicenseDetailsResponse?>
{
    private readonly LicenseDbContext _context;
    private readonly ILogger<GetLicenseByIdHandler> _logger;

    public GetLicenseByIdHandler(
        LicenseDbContext context,
        ILogger<GetLicenseByIdHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LicenseDetailsResponse?> Handle(
        GetLicenseByIdQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving license {LicenseId} for tenant {TenantId}",
            request.LicenseId,
            request.TenantId);

        // Prepare parameters
        var licenseIdParam = new SqlParameter("@LicenseId", request.LicenseId);
        var tenantIdParam = new SqlParameter("@TenantId", request.TenantId);

        // Call stored procedure
        var licenses = await _context.Licenses
            .FromSqlRaw(
                "EXEC sp_GetLicenseById @LicenseId, @TenantId",
                licenseIdParam,
                tenantIdParam)
            .ToListAsync(cancellationToken);

        var license = licenses.FirstOrDefault();

        if (license == null)
        {
            _logger.LogWarning(
                "License {LicenseId} not found for tenant {TenantId}",
                request.LicenseId,
                request.TenantId);
            return null;
        }

        // Parse history JSON
        List<HistoryEntry>? history = null;
        if (!string.IsNullOrEmpty(license.History))
        {
            try
            {
                history = JsonSerializer.Deserialize<List<HistoryEntry>>(
                    license.History,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse license history JSON");
            }
        }

        return new LicenseDetailsResponse
        {
            Id = license.Id,
            LicenseNumber = license.LicenseNumber,
            ApplicantName = license.ApplicantName,
            ApplicantEmail = license.ApplicantEmail,
            LicenseType = license.LicenseType,
            Status = license.Status,
            ExpiryDate = license.ExpiryDate,
            Amount = license.Amount,
            TenantId = license.TenantId,
            CreatedAt = license.CreatedAt,
            History = history ?? new List<HistoryEntry>()
        };
    }
}
