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
/// Handler for GetLicensesByTenantQuery.
/// Calls sp_GetLicensesByTenant stored procedure.
/// </summary>
public class GetLicensesByTenantHandler : IRequestHandler<GetLicensesByTenantQuery, List<LicenseDetailsResponse>>
{
    private readonly LicenseDbContext _context;
    private readonly ILogger<GetLicensesByTenantHandler> _logger;

    public GetLicensesByTenantHandler(
        LicenseDbContext context,
        ILogger<GetLicensesByTenantHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<LicenseDetailsResponse>> Handle(
        GetLicensesByTenantQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = !string.IsNullOrEmpty(request.UserRole) && 
                      request.UserRole.Contains("Admin", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Retrieving licenses for tenant {TenantId} - User: {UserEmail}, Role: {UserRole}, IsAdmin: {IsAdmin}, Filters: Status={Status}, ExpiringInDays={ExpiringInDays}",
            request.TenantId,
            request.UserEmail,
            request.UserRole,
            isAdmin,
            request.Status,
            request.ExpiringInDays);

        // Prepare parameters
        var tenantIdParam = new SqlParameter("@TenantId", request.TenantId);
        var statusParam = new SqlParameter("@Status", (object?)request.Status ?? DBNull.Value);
        var expiringParam = new SqlParameter("@ExpiringInDays", (object?)request.ExpiringInDays ?? DBNull.Value);

        try
        {
            // Call stored procedure
            var licenses = await _context.Licenses
                .FromSqlRaw(
                    "EXEC sp_GetLicensesByTenant @TenantId, @Status, @ExpiringInDays",
                    tenantIdParam,
                    statusParam,
                    expiringParam)
                .ToListAsync(cancellationToken);

            // Apply role-based filtering: Non-admins only see their own licenses
            if (!isAdmin && !string.IsNullOrEmpty(request.UserEmail))
            {
                licenses = licenses
                    .Where(l => l.ApplicantEmail.Equals(request.UserEmail, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogInformation(
                    "Filtered to {Count} licenses for user {UserEmail} (non-admin)",
                    licenses.Count,
                    request.UserEmail);
            }
            else
            {
                _logger.LogInformation(
                    "Retrieved {Count} licenses for tenant {TenantId} (admin view - all licenses)",
                    licenses.Count,
                    request.TenantId);
            }

            // Map to response DTOs
            return licenses.Select(license => new LicenseDetailsResponse
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
                History = ParseHistory(license.History)
            }).ToList();
        }
        catch (SqlException ex)
        {
            // If stored procedure doesn't exist, fall back to direct query
            _logger.LogWarning(ex, 
                "Stored procedure sp_GetLicensesByTenant not found, falling back to direct query");

            return await FallbackDirectQuery(request, cancellationToken);
        }
    }

    private async Task<List<LicenseDetailsResponse>> FallbackDirectQuery(
        GetLicensesByTenantQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = !string.IsNullOrEmpty(request.UserRole) && 
                      request.UserRole.Contains("Admin", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Using direct query for tenant {TenantId} - User: {UserEmail}, IsAdmin: {IsAdmin}",
            request.TenantId,
            request.UserEmail,
            isAdmin);

        var query = _context.Licenses
            .Where(l => l.TenantId == request.TenantId);

        // Apply role-based filtering: Non-admins only see their own licenses
        if (!isAdmin && !string.IsNullOrEmpty(request.UserEmail))
        {
            query = query.Where(l => l.ApplicantEmail == request.UserEmail);
        }

        // Apply status filter
        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(l => l.Status == request.Status);
        }

        // Apply expiring filter
        if (request.ExpiringInDays.HasValue)
        {
            var now = DateTime.UtcNow;
            var expiryDate = now.AddDays(request.ExpiringInDays.Value);
            query = query.Where(l => l.ExpiryDate >= now && l.ExpiryDate <= expiryDate);
        }

        var licenses = await query
            .OrderBy(l => l.ExpiryDate)
            .ToListAsync(cancellationToken);

        return licenses.Select(license => new LicenseDetailsResponse
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
            History = ParseHistory(license.History)
        }).ToList();
    }

    private List<HistoryEntry> ParseHistory(string? historyJson)
    {
        if (string.IsNullOrEmpty(historyJson))
        {
            return new List<HistoryEntry>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<HistoryEntry>>(
                historyJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                ?? new List<HistoryEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse license history JSON");
            return new List<HistoryEntry>();
        }
    }
}
