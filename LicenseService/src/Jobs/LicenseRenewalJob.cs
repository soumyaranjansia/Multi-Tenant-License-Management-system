using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Gov2Biz.LicenseService.Data;
using Gov2Biz.LicenseService.Models;

namespace Gov2Biz.LicenseService.Jobs;

/// <summary>
/// Hangfire job to check for expiring licenses and send notifications.
/// Runs daily to find licenses expiring within 30 days.
/// </summary>
public class LicenseRenewalJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LicenseRenewalJob> _logger;

    public LicenseRenewalJob(
        IServiceProvider serviceProvider,
        ILogger<LicenseRenewalJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Execute the daily renewal check job.
    /// Finds all licenses expiring in the next 30 days across all tenants.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting LicenseRenewalJob execution at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();

        try
        {
            // Get all active tenants (you would query Tenants table in production)
            var tenants = await GetActiveTenants(context);

            _logger.LogInformation("Found {TenantCount} active tenants to process", tenants.Count);

            foreach (var tenantId in tenants)
            {
                await ProcessTenantLicenses(context, tenantId);
            }

            _logger.LogInformation("LicenseRenewalJob completed successfully at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing LicenseRenewalJob");
            throw; // Hangfire will retry
        }
    }

    private async Task<List<string>> GetActiveTenants(LicenseDbContext context)
    {
        // In production, query Tenants table
        // For now, return distinct tenant IDs from Licenses table
        var tenants = await context.Licenses
            .Select(l => l.TenantId)
            .Distinct()
            .ToListAsync();

        return tenants;
    }

    private async Task ProcessTenantLicenses(LicenseDbContext context, string tenantId)
    {
        _logger.LogInformation("Processing licenses for tenant {TenantId}", tenantId);

        // Call sp_GetLicensesByTenant to find expiring licenses
        var tenantIdParam = new SqlParameter("@TenantId", tenantId);
        var statusParam = new SqlParameter("@Status", "Active");
        var expiringInDaysParam = new SqlParameter("@ExpiringInDays", 30);

        var expiringLicenses = await context.Licenses
            .FromSqlRaw(
                "EXEC sp_GetLicensesByTenant @TenantId, @Status, @ExpiringInDays",
                tenantIdParam,
                statusParam,
                expiringInDaysParam)
            .ToListAsync();

        _logger.LogInformation(
            "Found {Count} licenses expiring within 30 days for tenant {TenantId}",
            expiringLicenses.Count,
            tenantId);

        foreach (var license in expiringLicenses)
        {
            var daysUntilExpiry = (license.ExpiryDate - DateTime.UtcNow).Days;

            _logger.LogInformation(
                "License {LicenseNumber} expires in {Days} days - sending renewal notification",
                license.LicenseNumber,
                daysUntilExpiry);

            // Enqueue notification job
            // In production, this would call NotificationService API or publish event
            BackgroundJob.Enqueue(() => 
                SendRenewalNotification(license.ApplicantEmail, license.LicenseNumber, daysUntilExpiry));
        }
    }

    /// <summary>
    /// Send renewal notification to license holder.
    /// This is a placeholder - in production, call NotificationService API.
    /// </summary>
    public Task SendRenewalNotification(string email, string licenseNumber, int daysUntilExpiry)
    {
        _logger.LogInformation(
            "Sending renewal notification for license {LicenseNumber} to {Email} ({Days} days until expiry)",
            licenseNumber,
            email,
            daysUntilExpiry);

        // In production:
        // - Call NotificationService API: POST /api/notify/send
        // - Or publish RabbitMQ/Azure Service Bus message

        return Task.CompletedTask;
    }
}
