using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Dapper;
using Gov2Biz.LicenseService.Commands;
using Gov2Biz.LicenseService.Data;
using Gov2Biz.LicenseService.Models.DTOs;

namespace Gov2Biz.LicenseService.Handlers;

/// <summary>
/// Handler for CreateLicenseCommand.
/// Calls sp_CreateLicense stored procedure.
/// </summary>
public class CreateLicenseHandler : IRequestHandler<CreateLicenseCommand, CreateLicenseResponse>
{
    private readonly LicenseDbContext _context;
    private readonly ILogger<CreateLicenseHandler> _logger;

    public CreateLicenseHandler(
        LicenseDbContext context,
        ILogger<CreateLicenseHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CreateLicenseResponse> Handle(
        CreateLicenseCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating license for {ApplicantName} in tenant {TenantId} (Admin: {IsAdmin})",
            request.ApplicantName,
            request.TenantId,
            request.IsAdminCreated);

        // Prepare parameters for stored procedure
        var applicantNameParam = new SqlParameter("@ApplicantName", request.ApplicantName);
        var applicantEmailParam = new SqlParameter("@ApplicantEmail", request.ApplicantEmail);
        var licenseTypeParam = new SqlParameter("@LicenseType", request.LicenseType);
        var expiryDateParam = new SqlParameter("@ExpiryDate", request.ExpiryDate);
        var tenantIdParam = new SqlParameter("@TenantId", request.TenantId);
        var amountParam = new SqlParameter("@Amount", request.Amount);
        var metadataParam = new SqlParameter("@Metadata", 
            request.Metadata ?? (object)DBNull.Value);
        var isAdminCreatedParam = new SqlParameter("@IsAdminCreated", request.IsAdminCreated);

        // Call stored procedure
        var result = await _context.Database
            .SqlQueryRaw<StoredProcResult>(
                "EXEC sp_CreateLicense @ApplicantName, @ApplicantEmail, @LicenseType, @ExpiryDate, @TenantId, @Metadata, @Amount, @IsAdminCreated",
                applicantNameParam,
                applicantEmailParam,
                licenseTypeParam,
                expiryDateParam,
                tenantIdParam,
                metadataParam,
                amountParam,
                isAdminCreatedParam)
            .ToListAsync(cancellationToken);

        var spResult = result.FirstOrDefault();
        
        if (spResult == null)
            throw new InvalidOperationException("Failed to create license - stored procedure returned no result");

        // All licenses start as Pending regardless of who creates them
        // They will be activated after successful payment verification
        
        _logger.LogInformation(
            "License created successfully: ID={LicenseId}, Number={LicenseNumber}, Status=Pending, AdminCreated={IsAdmin}",
            spResult.NewId,
            spResult.LicenseNumber,
            request.IsAdminCreated);

        return new CreateLicenseResponse
        {
            Id = spResult.NewId,
            LicenseNumber = spResult.LicenseNumber,
            Status = "Pending", // Always start as Pending
            ExpiryDate = request.ExpiryDate,
            TenantId = request.TenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    // DTO for stored procedure result
    private class StoredProcResult
    {
        public int NewId { get; set; }
        public string LicenseNumber { get; set; } = string.Empty;
    }
}
