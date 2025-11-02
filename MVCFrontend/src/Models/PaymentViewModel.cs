namespace Gov2Biz.MVCFrontend.Models;

public class PaymentViewModel
{
    public int LicenseId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string RazorpayKey { get; set; } = string.Empty;
}

public class LicenseWithPayment
{
    public int Id { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal Amount { get; set; }
    public int? PaymentId { get; set; }
    public string? PaymentStatus { get; set; }
    public string? InvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PrintLicenseViewModel
{
    public LicenseDetails License { get; set; } = new();
    public PaymentDetails? Payment { get; set; }
    public string TenantName { get; set; } = string.Empty;
}

public class LicenseDetails
{
    public int Id { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentDetails
{
    public string? InvoiceId { get; set; }
    public string? TransactionId { get; set; }
    public DateTime? PaymentDate { get; set; }
}
