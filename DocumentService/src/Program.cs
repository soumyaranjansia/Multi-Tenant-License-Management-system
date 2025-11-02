using Gov2Biz.Shared.Context;
using Gov2Biz.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;
using System.ComponentModel.DataAnnotations;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "DocumentService")
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        path: "/Logs/documentservice-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Gov2Biz DocumentService");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Gov2Biz Document Service API",
            Version = "v1",
            Description = "Multi-tenant document management service with secure file storage"
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

    builder.Services.AddDbContext<DocumentDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddGov2BizShared();

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
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocumentService API v1"));
    }

    app.UseGov2BizMiddleware();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Ensure upload directory exists
    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Documents");
    if (!Directory.Exists(uploadsPath))
    {
        Directory.CreateDirectory(uploadsPath);
        Log.Information("Created documents directory at {Path}", uploadsPath);
    }

    Log.Information("DocumentService started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DocumentService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// DbContext
public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.TenantId, e.LicenseId });
        });
    }
}

// Models
public class Document
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int LicenseId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}

public record DocumentMetadata(int Id, string FileName, long FileSizeBytes, string ContentType, DateTime UploadedAt);
public record UploadRequest([Required] int LicenseId, [Required] IFormFile File);

// Controllers
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentDbContext _context;
    private readonly TenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentsController> _logger;
    private readonly string _uploadsPath;

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public DocumentsController(
        DocumentDbContext context,
        TenantContext tenantContext,
        IConfiguration configuration,
        ILogger<DocumentsController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
        _uploadsPath = Path.Combine(environment.ContentRootPath, "Documents");
    }

    /// <summary>
    /// Upload a document for a license
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<DocumentMetadata>> UploadDocument([FromForm] int licenseId, [FromForm] IFormFile file)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        if (file == null || file.Length == 0)
            return BadRequest(new { error = new { message = "No file provided", code = "INVALID_FILE" } });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { error = new { message = $"File type not allowed. Allowed: {string.Join(", ", AllowedExtensions)}", code = "INVALID_FILE_TYPE" } });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = new { message = $"File size exceeds {MaxFileSizeBytes / 1024 / 1024}MB limit", code = "FILE_TOO_LARGE" } });

        // Create tenant-specific directory
        var tenantPath = Path.Combine(_uploadsPath, tenantId, licenseId.ToString());
        Directory.CreateDirectory(tenantPath);

        // Generate unique filename
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(tenantPath, storedFileName);

        // Save file to disk
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Save metadata to database
        var document = new Document
        {
            TenantId = tenantId,
            LicenseId = licenseId,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = User.Identity?.Name ?? "system"
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document uploaded: {FileName} for LicenseId {LicenseId}, Size: {Size} bytes",
            file.FileName, licenseId, file.Length);

        return Ok(new DocumentMetadata(document.Id, document.FileName, document.FileSizeBytes, document.ContentType, document.UploadedAt));
    }

    /// <summary>
    /// Download a document by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (document == null)
            return NotFound(new { error = new { message = "Document not found", code = "DOCUMENT_NOT_FOUND" } });

        var filePath = Path.Combine(_uploadsPath, document.TenantId, document.LicenseId.ToString(), document.StoredFileName);

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogError("Document file not found on disk: {Path}", filePath);
            return NotFound(new { error = new { message = "Document file not found on storage", code = "FILE_NOT_FOUND" } });
        }

        var memory = new MemoryStream();
        using (var stream = new FileStream(filePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }
        memory.Position = 0;

        _logger.LogInformation("Document downloaded: {FileName} (ID: {Id})", document.FileName, id);

        return File(memory, document.ContentType, document.FileName);
    }

    /// <summary>
    /// List documents for a license
    /// </summary>
    [HttpGet("license/{licenseId}")]
    public async Task<ActionResult<List<DocumentMetadata>>> ListDocuments(int licenseId)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var documents = await _context.Documents
            .Where(d => d.LicenseId == licenseId && d.TenantId == tenantId)
            .Select(d => new DocumentMetadata(d.Id, d.FileName, d.FileSizeBytes, d.ContentType, d.UploadedAt))
            .ToListAsync();

        return Ok(documents);
    }

    /// <summary>
    /// Delete a document
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId not set");

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (document == null)
            return NotFound(new { error = new { message = "Document not found", code = "DOCUMENT_NOT_FOUND" } });

        var filePath = Path.Combine(_uploadsPath, document.TenantId, document.LicenseId.ToString(), document.StoredFileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document deleted: {FileName} (ID: {Id})", document.FileName, id);

        return Ok(new { message = "Document deleted successfully" });
    }
}

[ApiController]
[Route("healthz")]
public class HealthzController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "healthy", service = "DocumentService" });
}
