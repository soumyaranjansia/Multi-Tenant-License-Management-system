using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Gov2Biz.LicenseService.Commands;

namespace Gov2Biz.LicenseService.Handlers;

/// <summary>
/// Handler for updating license status (approval/rejection/suspension).
/// </summary>
public class UpdateLicenseStatusCommandHandler : IRequestHandler<UpdateLicenseStatusCommand, bool>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateLicenseStatusCommandHandler> _logger;

    public UpdateLicenseStatusCommandHandler(
        IConfiguration configuration,
        ILogger<UpdateLicenseStatusCommandHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateLicenseStatusCommand request, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Begin transaction
        using var transaction = connection.BeginTransaction();

        try
        {
            // Update license status
            var updateSql = @"
                UPDATE Licenses 
                SET Status = @Status, 
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @LicenseId 
                  AND TenantId = @TenantId";

            using (var updateCmd = new SqlCommand(updateSql, connection, transaction))
            {
                updateCmd.Parameters.AddWithValue("@Status", request.Status);
                updateCmd.Parameters.AddWithValue("@LicenseId", request.LicenseId);
                updateCmd.Parameters.AddWithValue("@TenantId", request.TenantId);

                var rowsAffected = await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                if (rowsAffected == 0)
                {
                    _logger.LogWarning(
                        "License {LicenseId} not found for tenant {TenantId}",
                        request.LicenseId,
                        request.TenantId);
                    return false;
                }
            }

            // Add history entry
            var historySql = @"
                INSERT INTO LicenseHistory (LicenseId, Action, PerformedBy, Details, Timestamp)
                VALUES (@LicenseId, @Action, @PerformedBy, @Details, SYSUTCDATETIME())";

            using (var historyCmd = new SqlCommand(historySql, connection, transaction))
            {
                var action = request.Status == "Active" ? "Approved" : 
                            request.Status == "Rejected" ? "Rejected" : 
                            "Status Changed";
                
                var details = string.IsNullOrEmpty(request.Reason) 
                    ? $"Status changed to {request.Status}" 
                    : $"Status changed to {request.Status}. Reason: {request.Reason}";

                historyCmd.Parameters.AddWithValue("@LicenseId", request.LicenseId);
                historyCmd.Parameters.AddWithValue("@Action", action);
                historyCmd.Parameters.AddWithValue("@PerformedBy", request.PerformedBy);
                historyCmd.Parameters.AddWithValue("@Details", details);

                await historyCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "License {LicenseId} status updated to {Status} by {PerformedBy}",
                request.LicenseId,
                request.Status,
                request.PerformedBy);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error updating license {LicenseId} status", request.LicenseId);
            throw;
        }
    }
}
