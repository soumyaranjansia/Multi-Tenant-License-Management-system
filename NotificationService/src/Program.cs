using Gov2Biz.Shared.Context;
using Gov2Biz.Shared.Extensions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MimeKit;
using Serilog;
using Serilog.Formatting.Compact;
using System.ComponentModel.DataAnnotations;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NotificationService")
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        path: "/Logs/notificationservice-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Gov2Biz NotificationService");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Gov2Biz Notification Service API",
            Version = "v1",
            Description = "Multi-tenant notification service with email support"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddGov2BizShared();
    builder.Services.AddScoped<IEmailService, EmailService>();

    var jwtSecret = builder.Configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret not configured");
    var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? "Gov2Biz";
    var jwtAudience = builder.Configuration["JWT:Audience"] ?? "Gov2Biz";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotificationService API v1"));
    }

    app.UseGov2BizMiddleware();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("NotificationService started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// DbContext
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.TenantId, e.Status });
        });
    }
}

// Models
public class Notification
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public record SendNotificationRequest(
    [Required] string Type,
    [Required][EmailAddress] string Recipient,
    [Required] string Subject,
    [Required] string Body);

public record NotificationResponse(int Id, string Status, DateTime CreatedAt, DateTime? SentAt);

// Email Service Interface
public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body);
}

// Email Service Implementation
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtpHost = _configuration["SMTP:Host"];
            var smtpPort = int.Parse(_configuration["SMTP:Port"] ?? "587");
            var smtpUsername = _configuration["SMTP:Username"];
            var smtpPassword = _configuration["SMTP:Password"];
            var fromEmail = _configuration["SMTP:FromEmail"] ?? "noreply@gov2biz.com";
            var fromName = _configuration["SMTP:FromName"] ?? "Gov2Biz";

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
            {
                _logger.LogWarning("SMTP not configured. Email simulation mode. Would send to: {To}, Subject: {Subject}", to, subject);
                await Task.Delay(100); // Simulate sending
                return true;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUsername, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return false;
        }
    }
}

// Controllers
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationDbContext _context;
    private readonly TenantContext _tenantContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationDbContext context,
        TenantContext tenantContext,
        IEmailService emailService,
        ILogger<NotificationsController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Send a notification
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<NotificationResponse>> SendNotification([FromBody] SendNotificationRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var notification = new Notification
        {
            TenantId = tenantId,
            Type = request.Type,
            Recipient = request.Recipient,
            Subject = request.Subject,
            Body = request.Body,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Send email asynchronously
        bool success = false;
        try
        {
            success = await _emailService.SendEmailAsync(request.Recipient, request.Subject, request.Body);
            notification.Status = success ? "Sent" : "Failed";
            notification.SentAt = success ? DateTime.UtcNow : null;
        }
        catch (Exception ex)
        {
            notification.Status = "Failed";
            notification.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to send notification {Id}", notification.Id);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Notification {Status}: {Type} to {Recipient}",
            notification.Status, request.Type, request.Recipient);

        return Ok(new NotificationResponse(notification.Id, notification.Status, notification.CreatedAt, notification.SentAt));
    }

    /// <summary>
    /// Get notification by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationResponse>> GetNotification(int id)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);

        if (notification == null)
            return NotFound(new { error = new { message = "Notification not found", code = "NOTIFICATION_NOT_FOUND" } });

        return Ok(new NotificationResponse(notification.Id, notification.Status, notification.CreatedAt, notification.SentAt));
    }

    /// <summary>
    /// List notifications with filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<NotificationResponse>>> ListNotifications([FromQuery] string? status = null)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var query = _context.Notifications.Where(n => n.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(n => n.Status == status);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationResponse(n.Id, n.Status, n.CreatedAt, n.SentAt))
            .ToListAsync();

        return Ok(notifications);
    }

    /// <summary>
    /// Enqueue notification for background processing (called by Hangfire)
    /// </summary>
    [HttpPost("enqueue")]
    [AllowAnonymous] // Internal service-to-service call
    public async Task<IActionResult> EnqueueNotification([FromBody] SendNotificationRequest request, [FromHeader(Name = "X-Tenant-ID")] string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new { error = new { message = "Tenant ID required", code = "MISSING_TENANT" } });

        var notification = new Notification
        {
            TenantId = tenantId,
            Type = request.Type,
            Recipient = request.Recipient,
            Subject = request.Subject,
            Body = request.Body,
            Status = "Queued",
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Notification enqueued: {Type} to {Recipient}", request.Type, request.Recipient);

        return Accepted(new { notificationId = notification.Id, message = "Notification queued for processing" });
    }
}

[ApiController]
[Route("healthz")]
public class HealthzController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "healthy", service = "NotificationService" });
}
