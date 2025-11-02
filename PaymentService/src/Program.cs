using Gov2Biz.Shared.Context;
using Gov2Biz.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Cryptography;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PaymentService")
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        path: "/Logs/paymentservice-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Gov2Biz PaymentService");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Add Swagger with JWT authentication
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Gov2Biz Payment Service API",
            Version = "v1",
            Description = "Multi-tenant payment processing service with webhook support"
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
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Database configuration
    builder.Services.AddDbContext<PaymentDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Add Gov2Biz Shared services (TenantContext)
    builder.Services.AddGov2BizShared();

    // JWT Authentication - Use same key as other services for consistency
    var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["JWT:Secret"] 
        ?? throw new InvalidOperationException("JWT key not configured (Jwt:Key or JWT:Secret required)");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["JWT:Issuer"] ?? "Gov2Biz";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["JWT:Audience"] ?? "Gov2Biz";

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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                RequireSignedTokens = true,
                ValidateTokenReplay = false
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentService API v1"));
    }

    // Use Gov2Biz global middlewares
    app.UseGov2BizMiddleware();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("PaymentService started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PaymentService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// PaymentDbContext
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PaymentNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => new { e.TenantId, e.PaymentNumber });
        });
    }
}

// Models
public class Payment
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int LicenseId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending";
    public string? GatewayTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// DTOs
public record CreatePaymentRequest(int LicenseId, decimal Amount, string Currency = "USD");
public record PaymentResponse(int Id, string PaymentNumber, decimal Amount, string Status, DateTime CreatedAt);
public record WebhookPayload(string TransactionId, string Status, string Signature);

// Controllers
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _context;
    private readonly TenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentDbContext context,
        TenantContext tenantContext,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Create a new payment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var paymentNumberParam = new SqlParameter("@PaymentNumber", System.Data.SqlDbType.NVarChar, 50) { Direction = System.Data.ParameterDirection.Output };
        var paymentIdParam = new SqlParameter("@PaymentId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };

        await _context.Database.ExecuteSqlRawAsync(
            "EXEC sp_CreatePayment @TenantId, @LicenseId, @Amount, @Currency, @PaymentNumber OUTPUT, @PaymentId OUTPUT",
            new SqlParameter("@TenantId", tenantId),
            new SqlParameter("@LicenseId", request.LicenseId),
            new SqlParameter("@Amount", request.Amount),
            new SqlParameter("@Currency", request.Currency),
            paymentNumberParam,
            paymentIdParam);

        var paymentNumber = paymentNumberParam.Value.ToString() ?? "";
        var paymentId = (int)paymentIdParam.Value;

        _logger.LogInformation("Payment created: {PaymentNumber} for LicenseId {LicenseId}", paymentNumber, request.LicenseId);

        return Ok(new PaymentResponse(paymentId, paymentNumber, request.Amount, "Pending", DateTime.UtcNow));
    }

    /// <summary>
    /// Get payment by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentResponse>> GetPayment(int id)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var payment = await _context.Payments
            .FromSqlRaw("EXEC sp_GetPaymentById @TenantId, @PaymentId",
                new SqlParameter("@TenantId", tenantId),
                new SqlParameter("@PaymentId", id))
            .FirstOrDefaultAsync();

        if (payment == null)
            return NotFound(new { error = new { message = "Payment not found", code = "PAYMENT_NOT_FOUND" } });

        return Ok(new PaymentResponse(payment.Id, payment.PaymentNumber, payment.Amount, payment.Status, payment.CreatedAt));
    }

    /// <summary>
    /// Payment gateway webhook (no authentication required)
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] WebhookPayload payload)
    {
        // Verify webhook signature
        var webhookSecret = _configuration["Webhook:Secret"] ?? "";
        if (!VerifyWebhookSignature(payload, webhookSecret))
        {
            _logger.LogWarning("Invalid webhook signature for transaction {TransactionId}", payload.TransactionId);
            return Unauthorized(new { error = new { message = "Invalid signature", code = "INVALID_SIGNATURE" } });
        }

        // Update payment status
        await _context.Database.ExecuteSqlRawAsync(
            "EXEC sp_UpdatePaymentStatus @TransactionId, @Status",
            new SqlParameter("@TransactionId", payload.TransactionId),
            new SqlParameter("@Status", payload.Status));

        _logger.LogInformation("Payment webhook processed for transaction {TransactionId}: {Status}", payload.TransactionId, payload.Status);

        return Ok(new { message = "Webhook processed successfully" });
    }

    private bool VerifyWebhookSignature(WebhookPayload payload, string secret)
    {
        var message = $"{payload.TransactionId}:{payload.Status}";
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message))).ToLower();
        return hash == payload.Signature.ToLower();
    }
}

[ApiController]
[Route("healthz")]
public class HealthzController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "healthy", service = "PaymentService" });
}
