using System.ComponentModel.DataAnnotations;

namespace Gov2Biz.PaymentService.Models;

public class CreatePaymentRequest
{
    [Required]
    public int LicenseId { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
    
    [Required]
    public string PaymentMethod { get; set; } = string.Empty; // "razorpay", "card", "upi", "netbanking"
    
    public int TenantId { get; set; }
}

public class RazorpayOrderResponse
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

public class VerifyPaymentRequest
{
    [Required]
    public int PaymentId { get; set; }
    
    [Required]
    public string RazorpayOrderId { get; set; } = string.Empty;
    
    [Required]
    public string RazorpayPaymentId { get; set; } = string.Empty;
    
    [Required]
    public string RazorpaySignature { get; set; } = string.Empty;
}

public class PaymentDetailsResponse
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

public class InvoiceDownloadRequest
{
    public int PaymentId { get; set; }
}

public class PrePaymentOrderRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
    
    [Required]
    public string LicenseType { get; set; } = string.Empty;
    
    [Required]
    public string ApplicantName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string ApplicantEmail { get; set; } = string.Empty;
}
