using Dapper;
using Gov2Biz.PaymentService.Models;
using Gov2Biz.Shared.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gov2Biz.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly ILogger<PaymentController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly string _razorpayKey;
    private readonly string _razorpaySecret;
    private readonly RazorpayClient _razorpayClient;

    public PaymentController(
        ILogger<PaymentController> logger,
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        _logger = logger;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _razorpayKey = configuration["Razorpay:KeyId"] ?? "rzp_test_RabTokVKFquXps";
        _razorpaySecret = configuration["Razorpay:KeySecret"] ?? "MTHFHYcXZu4O2f5Zjq4SqqDI";
        _razorpayClient = new RazorpayClient(_razorpayKey, _razorpaySecret);
    }

    /// <summary>
    /// Create a Razorpay order and invoice for payment
    /// </summary>
    [HttpPost("create-order")]
    [ProducesResponseType(typeof(RazorpayOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePaymentOrder([FromBody] CreatePaymentRequest request)
    {
        var tenantId = _tenantContext.TenantId 
            ?? throw new InvalidOperationException("TenantId not found");

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            // Get license and user details for invoice using stored procedure
            var licenseData = await connection.QueryFirstOrDefaultAsync(
                "sp_GetLicenseDataForPayment",
                new { LicenseId = request.LicenseId, TenantId = tenantId },
                commandType: System.Data.CommandType.StoredProcedure);

            if (licenseData == null)
            {
                return NotFound(new { error = "License not found" });
            }

            // Extract values safely
            string applicantName = licenseData.ApplicantName ?? "N/A";
            string email = licenseData.Email ?? "noreply@gov2biz.com";
            string licenseType = licenseData.TypeName ?? licenseData.LicenseType ?? "License";
            int durationMonths = licenseData.DurationMonths ?? 12;

            // Create Razorpay order
            var orderOptions = new Dictionary<string, object>
            {
                { "amount", (int)(request.Amount * 100) }, // Amount in paise
                { "currency", "INR" },
                { "receipt", $"LIC_{request.LicenseId}_{DateTime.UtcNow.Ticks}" },
                { "payment_capture", 1 }, // Auto capture
                { "notes", new Dictionary<string, string>
                    {
                        { "license_id", request.LicenseId.ToString() },
                        { "tenant_id", tenantId.ToString() },
                        { "license_type", licenseType },
                        { "applicant_name", applicantName }
                    }
                }
            };

            var order = _razorpayClient.Order.Create(orderOptions);
            string razorpayOrderId = order["id"]?.ToString() ?? string.Empty;

            _logger.LogInformation("Razorpay order created: {OrderId} for License: {LicenseId}", 
                razorpayOrderId, request.LicenseId);

            // Create Razorpay invoice for professional documentation
            string? razorpayInvoiceId = null;
            try
            {
                var invoiceOptions = new Dictionary<string, object>
                {
                    { "type", "invoice" },
                    { "description", $"{licenseType} License - {applicantName}" },
                    { "customer", new Dictionary<string, object>
                        {
                            { "name", applicantName },
                            { "email", email },
                            { "contact", "" }
                        }
                    },
                    { "line_items", new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                { "name", $"{licenseType} License" },
                                { "description", $"Valid for {durationMonths} months" },
                                { "amount", (int)(request.Amount * 100) },
                                { "currency", "INR" },
                                { "quantity", 1 }
                            }
                        }
                    },
                    { "currency", "INR" },
                    { "expire_by", DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds() }, // Invoice valid for 7 days
                    { "sms_notify", 0 },
                    { "email_notify", 0 }
                };

                var invoice = _razorpayClient.Invoice.Create(invoiceOptions);
                razorpayInvoiceId = invoice["id"]?.ToString();
                
                _logger.LogInformation("Razorpay invoice created: {InvoiceId}", razorpayInvoiceId ?? "N/A");
            }
            catch (Exception invoiceEx)
            {
                _logger.LogWarning(invoiceEx, "Failed to create Razorpay invoice, continuing without it");
            }

            // Create payment record in database
            var parameters = new DynamicParameters();
            parameters.Add("@LicenseId", request.LicenseId);
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@PaymentMethod", "razorpay"); // Default payment method
            parameters.Add("@TenantId", tenantId);
            parameters.Add("@RazorpayOrderId", razorpayOrderId);
            parameters.Add("@NewPaymentId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
            parameters.Add("@InvoiceId", dbType: System.Data.DbType.String, size: 100, direction: System.Data.ParameterDirection.Output);

            await connection.ExecuteAsync("sp_CreatePaymentWithInvoice", parameters, commandType: System.Data.CommandType.StoredProcedure);

            int paymentId = parameters.Get<int>("@NewPaymentId");
            string localInvoiceId = parameters.Get<string>("@InvoiceId");

            // Store Razorpay invoice ID if created
            if (!string.IsNullOrEmpty(razorpayInvoiceId))
            {
                await connection.ExecuteAsync(
                    "sp_UpdatePaymentRazorpaySignature",
                    new { PaymentId = paymentId, RazorpayInvoiceId = razorpayInvoiceId },
                    commandType: System.Data.CommandType.StoredProcedure);
            }

            _logger.LogInformation("Payment record created: ID={PaymentId}, LocalInvoice={LocalInvoice}, RazorpayInvoice={RazorpayInvoice}", 
                paymentId, localInvoiceId, razorpayInvoiceId);

            return Ok(new RazorpayOrderResponse
            {
                OrderId = razorpayOrderId,
                Amount = request.Amount,
                Currency = "INR",
                Key = _razorpayKey,
                PaymentId = paymentId,
                InvoiceId = localInvoiceId,
                RazorpayInvoiceId = razorpayInvoiceId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment order for License: {LicenseId}", request.LicenseId);
            return StatusCode(500, new { error = "Failed to create payment order", details = ex.Message });
        }
    }

    /// <summary>
    /// Verify Razorpay payment signature and complete payment
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        // Get tenant ID with fallback to header
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            tenantId = Request.Headers["X-Tenant-ID"].FirstOrDefault();
        }
        
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogError("TenantId not found in context or headers for payment verification");
            return BadRequest(new { error = "Tenant identification required" });
        }

        try
        {
            // Verify signature
            var signaturePayload = $"{request.RazorpayOrderId}|{request.RazorpayPaymentId}";
            var expectedSignature = CalculateSignature(signaturePayload, _razorpaySecret);

            if (expectedSignature != request.RazorpaySignature)
            {
                _logger.LogWarning("Invalid payment signature for PaymentId: {PaymentId}", request.PaymentId);
                return BadRequest(new { error = "Invalid payment signature" });
            }

            _logger.LogInformation("Payment signature verified for PaymentId: {PaymentId}", request.PaymentId);

            // Update payment status in database
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            await connection.ExecuteAsync(
                "sp_CompletePayment",
                new
                {
                    PaymentId = request.PaymentId,
                    RazorpayPaymentId = request.RazorpayPaymentId,
                    RazorpaySignature = request.RazorpaySignature,
                    TransactionId = request.RazorpayPaymentId
                },
                commandType: System.Data.CommandType.StoredProcedure);

            _logger.LogInformation("Payment completed successfully: PaymentId={PaymentId}, TransactionId={TransactionId}", 
                request.PaymentId, request.RazorpayPaymentId);

            return Ok(new
            {
                success = true,
                message = "Payment verified and completed successfully",
                paymentId = request.PaymentId,
                transactionId = request.RazorpayPaymentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment: PaymentId={PaymentId}", request.PaymentId);
            return StatusCode(500, new { error = "Failed to verify payment", details = ex.Message });
        }
    }

    /// <summary>
    /// Get payment details by license ID
    /// </summary>
    [HttpGet("license/{licenseId}")]
    [ProducesResponseType(typeof(PaymentDetailsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentByLicenseId(int licenseId)
    {
        // Get tenant ID with fallback to header
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            tenantId = Request.Headers["X-Tenant-ID"].FirstOrDefault();
        }
        
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("TenantId not found for GetPaymentByLicenseId, proceeding anyway");
            // Don't fail - just proceed without tenant filtering
        }

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            var payment = await connection.QueryFirstOrDefaultAsync<PaymentDetailsResponse>(
                "sp_GetPaymentByLicenseId",
                new { LicenseId = licenseId },
                commandType: System.Data.CommandType.StoredProcedure);

            if (payment == null)
            {
                return NotFound(new { error = "Payment not found for this license" });
            }

            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payment for License: {LicenseId}", licenseId);
            return StatusCode(500, new { error = "Failed to fetch payment details" });
        }
    }

    /// <summary>
    /// Download invoice for a payment
    /// </summary>
    [HttpGet("invoice/{paymentId}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadInvoice(int paymentId)
    {
        // Get tenant ID with fallback to header
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            tenantId = Request.Headers["X-Tenant-ID"].FirstOrDefault();
        }

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            // Get payment with license details using stored procedure
            var payment = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "sp_GetPaymentWithLicenseDetails",
                new { PaymentId = paymentId, TenantId = tenantId },
                commandType: System.Data.CommandType.StoredProcedure);

            if (payment == null)
            {
                return NotFound(new { error = "Invoice not found" });
            }

            // Generate HTML invoice
            var html = GenerateInvoiceHtml(payment);
            var bytes = Encoding.UTF8.GetBytes(html);

            return File(bytes, "text/html", $"Invoice_{payment.InvoiceId}.html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading invoice for PaymentId: {PaymentId}", paymentId);
            return StatusCode(500, new { error = "Failed to download invoice" });
        }
    }

    private string CalculateSignature(string payload, string secret)
    {
        var encoding = new UTF8Encoding();
        var keyBytes = encoding.GetBytes(secret);
        var messageBytes = encoding.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// Create a payment order before license creation (pre-payment flow)
    /// </summary>
    [HttpPost("create-pre-order")]
    [ProducesResponseType(typeof(RazorpayOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePrePaymentOrder([FromBody] PrePaymentOrderRequest request)
    {
        // Try to get tenant ID from context first, then from header
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId) && Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader))
        {
            tenantId = tenantHeader.ToString();
        }
        
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Tenant ID not found in context or headers");
            return BadRequest(new { error = "Tenant ID is required" });
        }

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            // Create Razorpay order
            var orderOptions = new Dictionary<string, object>
            {
                { "amount", (int)(request.Amount * 100) }, // Amount in paise
                { "currency", "INR" },
                { "receipt", $"PRE_{DateTime.UtcNow.Ticks}" },
                { "payment_capture", 1 }, // Auto capture
                { "notes", new Dictionary<string, string>
                    {
                        { "tenant_id", tenantId.ToString() },
                        { "license_type", request.LicenseType },
                        { "applicant_name", request.ApplicantName },
                        { "applicant_email", request.ApplicantEmail }
                    }
                }
            };

            var order = _razorpayClient.Order.Create(orderOptions);
            string razorpayOrderId = order["id"]?.ToString() ?? string.Empty;

            _logger.LogInformation("Pre-payment Razorpay order created: {OrderId} for {ApplicantEmail}", 
                razorpayOrderId, request.ApplicantEmail);

            // Create a payment record with LicenseId = 0 (to be updated later)
            var parameters = new DynamicParameters();
            parameters.Add("@LicenseId", 0); // Temporary - will be updated after license creation
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@PaymentMethod", "razorpay");
            parameters.Add("@TenantId", tenantId);
            parameters.Add("@RazorpayOrderId", razorpayOrderId);
            parameters.Add("@NewPaymentId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
            parameters.Add("@InvoiceId", dbType: System.Data.DbType.String, size: 100, direction: System.Data.ParameterDirection.Output);

            await connection.ExecuteAsync("sp_CreatePaymentWithInvoice", parameters, commandType: System.Data.CommandType.StoredProcedure);

            int paymentId = parameters.Get<int>("@NewPaymentId");
            string localInvoiceId = parameters.Get<string>("@InvoiceId");

            _logger.LogInformation("Pre-payment record created: ID={PaymentId}, LocalInvoice={LocalInvoice}", 
                paymentId, localInvoiceId);

            return Ok(new RazorpayOrderResponse
            {
                OrderId = razorpayOrderId,
                Amount = request.Amount,
                Currency = "INR",
                Key = _razorpayKey,
                PaymentId = paymentId,
                InvoiceId = localInvoiceId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pre-payment order for {ApplicantEmail}", request.ApplicantEmail);
            return StatusCode(500, new { error = "Failed to create payment order", details = ex.Message });
        }
    }



    /// <summary>
    /// Link a payment to a license after license creation
    /// </summary>
    [HttpPut("{paymentId}/link-license/{licenseId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkPaymentToLicense(int paymentId, int licenseId)
    {
        // Get tenant ID with fallback to header
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            tenantId = Request.Headers["X-Tenant-ID"].FirstOrDefault();
        }

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            // Link payment to license using stored procedure
            var result = await connection.QueryFirstOrDefaultAsync(
                "sp_LinkPaymentToLicense",
                new { PaymentId = paymentId, LicenseId = licenseId, TenantId = tenantId },
                commandType: System.Data.CommandType.StoredProcedure);

            if (result == null)
            {
                return NotFound(new { error = "License not found" });
            }

            _logger.LogInformation("Payment {PaymentId} linked to License {LicenseId}", paymentId, licenseId);

            return Ok(new { message = "Payment linked to license successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking payment {PaymentId} to license {LicenseId}", paymentId, licenseId);
            return StatusCode(500, new { error = "Failed to link payment to license" });
        }
    }

    private string GenerateInvoiceHtml(dynamic payment)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Invoice - {payment.InvoiceId}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #2c3e50; margin: 0; }}
        .header p {{ color: #7f8c8d; margin: 5px 0; }}
        .invoice-details {{ margin: 20px 0; padding: 20px; background: #f8f9fa; border-radius: 5px; }}
        .invoice-details table {{ width: 100%; }}
        .invoice-details td {{ padding: 8px; }}
        .invoice-details td:first-child {{ font-weight: bold; width: 30%; }}
        .items {{ margin: 20px 0; }}
        .items table {{ width: 100%; border-collapse: collapse; }}
        .items th {{ background: #3498db; color: white; padding: 12px; text-align: left; }}
        .items td {{ padding: 12px; border-bottom: 1px solid #ddd; }}
        .total {{ text-align: right; font-size: 20px; font-weight: bold; margin: 20px 0; }}
        .footer {{ margin-top: 40px; text-align: center; color: #7f8c8d; font-size: 12px; }}
        .paid {{ color: #27ae60; font-weight: bold; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🏛️ Gov2Biz License System</h1>
        <p>License Payment Invoice</p>
        <p style=""font-size: 24px; font-weight: bold; color: #3498db;"">{payment.InvoiceId}</p>
    </div>

    <div class=""invoice-details"">
        <table>
            <tr>
                <td>Invoice Date:</td>
                <td>{payment.CreatedAt:dd MMM yyyy}</td>
            </tr>
            <tr>
                <td>License Number:</td>
                <td>{payment.LicenseNumber ?? "N/A"}</td>
            </tr>
            <tr>
                <td>Razorpay Order ID:</td>
                <td>{payment.RazorpayOrderId ?? "N/A"}</td>
            </tr>
            <tr>
                <td>Razorpay Payment ID:</td>
                <td>{payment.RazorpayPaymentId ?? "N/A"}</td>
            </tr>
            <tr>
                <td>Status:</td>
                <td class=""paid"">{payment.Status}</td>
            </tr>
        </table>
    </div>

    <div class=""invoice-details"">
        <h3>Customer Details</h3>
        <table>
            <tr>
                <td>Name:</td>
                <td>{payment.ApplicantName ?? "N/A"}</td>
            </tr>
            <tr>
                <td>Email:</td>
                <td>{payment.ApplicantEmail ?? "N/A"}</td>
            </tr>
            <tr>
                <td>Tenant ID:</td>
                <td>{payment.TenantId ?? "N/A"}</td>
            </tr>
        </table>
    </div>

    <div class=""items"">
        <table>
            <thead>
                <tr>
                    <th>Description</th>
                    <th>License Period</th>
                    <th style=""text-align: right;"">Amount</th>
                </tr>
            </thead>
            <tbody>
                <tr>
                    <td>{payment.LicenseType} License</td>
                    <td>{payment.IssueDate:dd MMM yyyy} - {payment.ExpiryDate:dd MMM yyyy}</td>
                    <td style=""text-align: right;"">₹{payment.Amount:N2}</td>
                </tr>
            </tbody>
        </table>
    </div>

    <div class=""total"">
        Total Paid: <span style=""color: #27ae60;"">₹{payment.Amount:N2}</span>
    </div>

    <div class=""footer"">
        <p>Thank you for your payment!</p>
        <p>This is a computer-generated invoice and does not require a signature.</p>
        <p>Generated on {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC</p>
    </div>
</body>
</html>";
    }
}
