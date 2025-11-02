namespace Gov2Biz.PaymentService.Models;

public class CreateAdminPaymentRequest
{
    public int LicenseId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AdminPaymentResponse
{
    public int Id { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
}