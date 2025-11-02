using Microsoft.AspNetCore.Mvc;
using Gov2Biz.MVCFrontend.Models;

namespace Gov2Biz.MVCFrontend.Controllers;

public class LicensesController : Controller
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<LicensesController> _logger;

    public LicensesController(IApiClient apiClient, ILogger<LicensesController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    private bool IsAuthenticated()
    {
        var token = HttpContext.Session.GetString("JwtToken");
        return !string.IsNullOrEmpty(token);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var tenantId = HttpContext.Session.GetString("TenantId");
            // Use the correct endpoint: GET /license (tenant ID is in X-Tenant-ID header)
            var licenses = await _apiClient.GetAsync<List<LicenseDto>>("/license");
            
            ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
            ViewBag.TenantId = tenantId;
            
            return View(licenses ?? new List<LicenseDto>());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to load licenses: {ex.Message}";
            return View(new List<LicenseDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        var model = new CreateLicenseRequest
        {
            ApplicantEmail = HttpContext.Session.GetString("UserEmail") ?? "",
            ExpiryDate = DateTime.UtcNow.AddMonths(12) // Default to 12 months
        };

        try
        {
            // Get license types for dropdown
            var licenseTypes = await _apiClient.GetAsync<List<LicenseTypeDto>>("/license/types");
            ViewBag.LicenseTypes = licenseTypes ?? new List<LicenseTypeDto>();

            // Check if user is admin
            var userRole = HttpContext.Session.GetString("UserRoles");
            var isAdmin = !string.IsNullOrEmpty(userRole) && userRole.Contains("Admin", StringComparison.OrdinalIgnoreCase);
            ViewBag.IsAdmin = isAdmin;

            // If admin, get users for assignment
            if (isAdmin)
            {
                var users = await _apiClient.GetAsync<List<UserDto>>("/license/users");
                ViewBag.Users = users ?? new List<UserDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading create license page data");
            ViewBag.LicenseTypes = new List<LicenseTypeDto>();
            ViewBag.Users = new List<UserDto>();
            ViewBag.IsAdmin = false;
        }
        
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateLicenseRequest model)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for Create License");
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                _logger.LogWarning("Validation error: {Error}", error.ErrorMessage);
            }
            return View(model);
        }

        try
        {
            _logger.LogInformation("Creating license for {ApplicantEmail}, Type: {LicenseType}, Amount: {Amount}", 
                model.ApplicantEmail, model.LicenseType, model.Amount);

            var response = await _apiClient.PostAsync<LicenseDto>("/license", model);
            
            _logger.LogInformation("License creation response: Id={Id}, LicenseNumber={LicenseNumber}, Status={Status}", 
                response?.Id, response?.LicenseNumber, response?.Status);
            
            if (response != null && response.Id > 0)
            {
                // Check if user is admin
                var userRole = HttpContext.Session.GetString("UserRoles");
                var isAdmin = !string.IsNullOrEmpty(userRole) && userRole.Contains("Admin", StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation("User role check: UserRole={UserRole}, IsAdmin={IsAdmin}", userRole, isAdmin);

                // Both admin and regular users now go through payment process
                _logger.LogInformation("User ({UserRole}) - redirecting to Payment page for license {LicenseId}", 
                    userRole ?? "Unknown", response.Id);
                
                if (isAdmin)
                {
                    TempData["InfoMessage"] = "License draft created. Please complete payment to activate the license.";
                }
                else
                {
                    TempData["InfoMessage"] = "License created. Please complete payment to proceed.";
                }
                
                return RedirectToAction("Payment", new { id = response.Id });
            }
            
            _logger.LogWarning("License creation failed: response is null or Id <= 0");
            ModelState.AddModelError("", "Failed to create license");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating license: {Message}", ex.Message);
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var license = await _apiClient.GetAsync<LicenseDto>($"/license/{id}");
            
            if (license == null)
            {
                TempData["ErrorMessage"] = "License not found";
                return RedirectToAction("Index");
            }

            // Get documents for this license
            var documents = await _apiClient.GetAsync<List<DocumentDto>>($"/document/license/{id}");
            ViewBag.Documents = documents ?? new List<DocumentDto>();
            
            // Get payment details for this license
            try
            {
                var payment = await _apiClient.GetAsync<PaymentDetailsDto>($"/payment/license/{id}");
                ViewBag.Payment = payment;
            }
            catch
            {
                // Payment might not exist yet, that's okay
                ViewBag.Payment = null;
            }
            
            return View(license);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to load license: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Renew(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var request = new RenewLicenseRequest { Months = 12 };
            var response = await _apiClient.PutAsync<LicenseDto>($"/license/{id}/renew", request);
            
            if (response != null)
            {
                TempData["SuccessMessage"] = "License renewed successfully!";
                return RedirectToAction("Details", new { id });
            }
            
            TempData["ErrorMessage"] = "Failed to renew license";
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(int licenseId, IFormFile file)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file";
            return RedirectToAction("Details", new { id = licenseId });
        }

        try
        {
            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(file.OpenReadStream());
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "file", file.FileName);
            content.Add(new StringContent(licenseId.ToString()), "licenseId");
            content.Add(new StringContent(HttpContext.Session.GetString("TenantId") ?? ""), "tenantId");

            var response = await _apiClient.PostFileAsync("/document/upload", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Document uploaded successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to upload document";
            }
            
            return RedirectToAction("Details", new { id = licenseId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id = licenseId });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDocument(int documentId, int licenseId)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var success = await _apiClient.DeleteAsync($"/document/{documentId}");
            
            if (success)
            {
                TempData["SuccessMessage"] = "Document deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete document";
            }
            
            return RedirectToAction("Details", new { id = licenseId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id = licenseId });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id, string? reason)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var request = new UpdateLicenseStatusRequest 
            { 
                Status = "Active",
                Reason = reason ?? "Approved by administrator"
            };
            
            var response = await _apiClient.PutAsync<object>($"/license/{id}/status", request);
            
            if (response != null)
            {
                TempData["SuccessMessage"] = "License approved successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to approve license";
            }
            
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var request = new UpdateLicenseStatusRequest 
            { 
                Status = "Rejected",
                Reason = reason ?? "Rejected by administrator"
            };
            
            var response = await _apiClient.PutAsync<object>($"/license/{id}/status", request);
            
            if (response != null)
            {
                TempData["SuccessMessage"] = "License rejected!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to reject license";
            }
            
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Deactivate(int id, string? reason)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var request = new UpdateLicenseStatusRequest 
            { 
                Status = "Pending",
                Reason = reason ?? "Deactivated by administrator"
            };
            
            var response = await _apiClient.PutAsync<object>($"/license/{id}/status", request);
            
            if (response != null)
            {
                TempData["SuccessMessage"] = "License deactivated successfully! Status changed to Pending.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to deactivate license";
            }
            
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Activate(int id, string? reason)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var request = new UpdateLicenseStatusRequest 
            { 
                Status = "Active",
                Reason = reason ?? "Activated by administrator"
            };
            
            var response = await _apiClient.PutAsync<object>($"/license/{id}/status", request);
            
            if (response != null)
            {
                TempData["SuccessMessage"] = "License activated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to activate license";
            }
            
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpGet]
    public IActionResult DownloadDocument(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            // Redirect to API endpoint for document download
            return Redirect($"http://localhost:5000/api/document/{id}");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    public async Task<IActionResult> DownloadInvoice(int paymentId, int licenseId)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            // Get payment and license details
            var payment = await _apiClient.GetAsync<PaymentDetailsDto>($"/payment/license/{licenseId}");
            var license = await _apiClient.GetAsync<LicenseDto>($"/license/{licenseId}");
            
            if (payment == null || license == null)
            {
                TempData["ErrorMessage"] = "Payment or license not found";
                return RedirectToAction("Details", new { id = licenseId });
            }

            // Pass data to invoice view
            ViewBag.Payment = payment;
            ViewBag.License = license;
            
            return View("Invoice");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error generating invoice: {ex.Message}";
            return RedirectToAction("Details", new { id = licenseId });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var success = await _apiClient.DeleteAsync($"/license/{id}");
            
            if (success)
            {
                TempData["SuccessMessage"] = "License deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete license";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Payment(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var license = await _apiClient.GetAsync<LicenseDto>($"/license/{id}");
            
            if (license == null)
            {
                TempData["ErrorMessage"] = "License not found";
                return RedirectToAction("Index");
            }

            // Check if payment already exists
            try
            {
                var existingPayment = await _apiClient.GetAsync<PaymentDetailsDto>($"/payment/license/{id}");
                if (existingPayment != null && existingPayment.PaymentStatus == "Completed")
                {
                    TempData["InfoMessage"] = "Payment already completed for this license";
                    return RedirectToAction("Details", new { id });
                }
            }
            catch
            {
                // No existing payment found, continue
            }

            var model = new PaymentViewModel
            {
                LicenseId = license.Id,
                LicenseType = license.LicenseType,
                LicenseNumber = license.LicenseNumber,
                ApplicantName = license.ApplicantName,
                Amount = license.Amount,
                IssueDate = license.CreatedAt,
                ExpiryDate = license.ExpiryDate,
                UserEmail = license.ApplicantEmail,
                RazorpayKey = "rzp_test_RabTokVKFquXps" // Should come from configuration
            };

            _logger.LogInformation("Loading payment page for license {LicenseId}, Amount: {Amount}", id, license.Amount);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading payment page for license {LicenseId}", id);
            TempData["ErrorMessage"] = $"Failed to load payment page: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePaymentOrder([FromBody] CreatePaymentOrderRequest request)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, error = "Not authenticated" });
        }

        try
        {
            var paymentRequest = new CreatePaymentRequestDto
            {
                LicenseId = request.LicenseId,
                Amount = request.Amount
            };

            var response = await _apiClient.PostAsync<RazorpayOrderResponseDto>("/payment/create-order", paymentRequest);
            
            if (response != null)
            {
                return Json(new { success = true, data = response });
            }

            return Json(new { success = false, error = "Failed to create payment order" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment order");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto request)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, error = "Not authenticated" });
        }

        try
        {
            var response = await _apiClient.PostAsync<object>("/payment/verify", request);
            
            if (response != null)
            {
                return Json(new { success = true, message = "Payment verified successfully" });
            }

            return Json(new { success = false, error = "Payment verification failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        try
        {
            var license = await _apiClient.GetAsync<LicenseDto>($"/license/{id}");
            
            if (license == null)
            {
                TempData["ErrorMessage"] = "License not found";
                return RedirectToAction("Index");
            }

            // Only allow printing for Active licenses
            if (license.Status != "Active")
            {
                TempData["ErrorMessage"] = "Only active licenses can be printed";
                return RedirectToAction("Details", new { id });
            }

            // Get payment details if available
            PaymentDetailsDto? payment = null;
            try
            {
                payment = await _apiClient.GetAsync<PaymentDetailsDto>($"/payment/license/{id}");
            }
            catch
            {
                // Payment might not exist for admin-created licenses
            }

            var model = new PrintLicenseViewModel
            {
                License = new LicenseDetails
                {
                    Id = license.Id,
                    LicenseNumber = license.LicenseNumber,
                    ApplicantName = license.ApplicantName,
                    ApplicantEmail = license.ApplicantEmail,
                    LicenseType = license.LicenseType,
                    Status = license.Status,
                    IssueDate = license.IssueDate,
                    ExpiryDate = license.ExpiryDate,
                    Amount = license.Amount
                },
                Payment = payment != null ? new PaymentDetails
                {
                    InvoiceId = payment.InvoiceId,
                    TransactionId = payment.TransactionId,
                    PaymentDate = payment.CreatedAt
                } : null,
                TenantName = HttpContext.Session.GetString("TenantId") ?? "Gov2Biz"
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading print view for license {LicenseId}", id);
            TempData["ErrorMessage"] = $"Failed to load license: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [HttpGet]
    public IActionResult PaymentPreview()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Index", "Login");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreatePrePaymentOrder([FromBody] PrePaymentOrderRequest request)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, error = "Not authenticated" });
        }

        try
        {
            _logger.LogInformation("Creating pre-payment order for {ApplicantEmail}, Type: {LicenseType}, Amount: {Amount}",
                request.ApplicantEmail, request.LicenseType, request.Amount);

            // Create a temporary payment order without a license ID
            var orderData = new
            {
                amount = request.Amount,
                currency = "INR",
                licenseType = request.LicenseType,
                applicantName = request.ApplicantName,
                applicantEmail = request.ApplicantEmail
            };

            var response = await _apiClient.PostAsync<RazorpayOrderResponseDto>("/payment/create-pre-order", orderData);

            if (response != null)
            {
                _logger.LogInformation("Pre-payment order created: OrderId={OrderId}, PaymentId={PaymentId}",
                    response.OrderId, response.PaymentId);
                return Json(new { success = true, data = response });
            }

            return Json(new { success = false, error = "Failed to create payment order" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pre-payment order");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAfterPayment([FromBody] CreateLicenseAfterPaymentRequest request)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, error = "Not authenticated" });
        }

        try
        {
            _logger.LogInformation("Creating license after payment for {ApplicantEmail}, PaymentId: {PaymentId}",
                request.ApplicantEmail, request.RazorpayPaymentId);

            // First verify the payment
            var verifyRequest = new VerifyPaymentDto
            {
                PaymentId = request.PaymentId,
                RazorpayOrderId = request.RazorpayOrderId,
                RazorpayPaymentId = request.RazorpayPaymentId,
                RazorpaySignature = request.RazorpaySignature
            };

            var verifyResponse = await _apiClient.PostAsync<object>("/payment/verify", verifyRequest);

            if (verifyResponse == null)
            {
                return Json(new { success = false, error = "Payment verification failed" });
            }

            // Now create the license
            var licenseRequest = new CreateLicenseRequest
            {
                ApplicantName = request.ApplicantName,
                ApplicantEmail = request.ApplicantEmail,
                LicenseType = request.LicenseType,
                Amount = request.Amount,
                ExpiryDate = DateTime.Parse(request.ExpiryDate)
            };

            var licenseResponse = await _apiClient.PostAsync<LicenseDto>("/license", licenseRequest);

            if (licenseResponse != null && licenseResponse.Id > 0)
            {
                _logger.LogInformation("License created successfully after payment: LicenseId={LicenseId}, PaymentId={PaymentId}",
                    licenseResponse.Id, request.RazorpayPaymentId);

                // Update payment with license ID
                await _apiClient.PutAsync<object>($"/payment/{request.PaymentId}/link-license/{licenseResponse.Id}", new { });

                return Json(new
                {
                    success = true,
                    licenseId = licenseResponse.Id,
                    licenseNumber = licenseResponse.LicenseNumber
                });
            }

            return Json(new { success = false, error = "Failed to create license" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating license after payment");
            return Json(new { success = false, error = ex.Message });
        }
    }
}

// DTOs
public class LicenseDto
{
    public int Id { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    
    // Helper properties for UI
    public DateTime IssueDate => CreatedAt;
}

public class CreateLicenseRequest
{
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 100.00m;
    public DateTime ExpiryDate { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class LicenseTypeDto
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public int DurationMonths { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
}

public class UpdateLicenseStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class RenewLicenseRequest
{
    public int Months { get; set; } = 12;
}

public class DocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class CreatePaymentOrderRequest
{
    public int LicenseId { get; set; }
    public decimal Amount { get; set; }
}

public class CreatePaymentRequestDto
{
    public int LicenseId { get; set; }
    public decimal Amount { get; set; }
}

public class RazorpayOrderResponseDto
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Key { get; set; } = string.Empty;
    public int PaymentId { get; set; }
    public string InvoiceId { get; set; } = string.Empty;
    public string? RazorpayInvoiceId { get; set; }
    public string? RazorpayInvoiceUrl { get; set; }
}

public class VerifyPaymentDto
{
    public int PaymentId { get; set; }
    public string RazorpayOrderId { get; set; } = string.Empty;
    public string RazorpayPaymentId { get; set; } = string.Empty;
    public string RazorpaySignature { get; set; } = string.Empty;
}

public class PaymentDetailsDto
{
    public int Id { get; set; }
    public int LicenseId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? InvoiceId { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ApplicantName { get; set; }
    public string? LicenseType { get; set; }
    public string? LicenseStatus { get; set; }
}

public class CreateAdminPaymentRequest
{
    public int LicenseId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class PaymentResponseDto
{
    public int Id { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PrePaymentOrderRequest
{
    public decimal Amount { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
}

public class CreateLicenseAfterPaymentRequest
{
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ExpiryDate { get; set; } = string.Empty;
    public int PaymentId { get; set; }
    public string RazorpayOrderId { get; set; } = string.Empty;
    public string RazorpayPaymentId { get; set; } = string.Empty;
    public string RazorpaySignature { get; set; } = string.Empty;
}
