-- Seed Data for Gov2Biz
-- Provides initial test data for development
-- ==============================================

USE Gov2Biz;
GO

-- Insert test tenants
INSERT INTO Tenants (TenantId, Name, IsActive) VALUES
('tenant_local', 'Local Development Tenant', 1),
('tenant_demo', 'Demo Organization', 1),
('tenant_test', 'Test Agency', 1);

-- Insert test users (password is 'Password123!' hashed with BCrypt)
-- Note: In production, use proper password hashing
INSERT INTO Users (Username, Email, PasswordHash, Roles, TenantId, IsActive) VALUES
('admin@local', 'admin@local.com', '$2a$11$JZxE.9Kt7p9Q5Y8nZ1XQ0.vF4K1J4F9Z7Y8nZ1XQ0vF4K1J4F9Z7Y', 'Admin', 'tenant_local', 1),
('applicant@local', 'applicant@local.com', '$2a$11$JZxE.9Kt7p9Q5Y8nZ1XQ0.vF4K1J4F9Z7Y8nZ1XQ0vF4K1J4F9Z7Y', 'Applicant', 'tenant_local', 1),
('agency@local', 'agency@local.com', '$2a$11$JZxE.9Kt7p9Q5Y8nZ1XQ0.vF4K1J4F9Z7Y8nZ1XQ0vF4K1J4F9Z7Y', 'AgencyUser', 'tenant_local', 1),
('demo@demo', 'demo@demo.com', '$2a$11$JZxE.9Kt7p9Q5Y8nZ1XQ0.vF4K1J4F9Z7Y8nZ1XQ0vF4K1J4F9Z7Y', 'Admin,Applicant', 'tenant_demo', 1);

-- Insert sample licenses
INSERT INTO Licenses (LicenseNumber, ApplicantName, ApplicantEmail, LicenseType, Status, ExpiryDate, TenantId) VALUES
('LIC-2025-000001', 'John Doe', 'john.doe@example.com', 'Business License', 'Active', DATEADD(YEAR, 1, GETUTCDATE()), 'tenant_local'),
('LIC-2025-000002', 'Jane Smith', 'jane.smith@example.com', 'Professional License', 'Active', DATEADD(MONTH, 6, GETUTCDATE()), 'tenant_local'),
('LIC-2025-000003', 'Bob Johnson', 'bob.johnson@example.com', 'Vendor License', 'Pending', DATEADD(YEAR, 2, GETUTCDATE()), 'tenant_local'),
('LIC-2025-000004', 'Alice Brown', 'alice.brown@example.com', 'Business License', 'Active', DATEADD(DAY, 30, GETUTCDATE()), 'tenant_demo');

-- Insert license history
INSERT INTO LicenseHistory (LicenseId, Action, PerformedBy, Details) VALUES
(1, 'Created', 'admin@local.com', 'Initial license creation'),
(2, 'Created', 'admin@local.com', 'Initial license creation'),
(3, 'Created', 'applicant@local.com', 'Application submitted'),
(4, 'Created', 'demo@demo.com', 'Demo license created');

-- Insert sample payments
INSERT INTO Payments (PaymentReference, Amount, Currency, Status, Metadata, TenantId) VALUES
('PAY-2025-001', 100.00, 'USD', 'Succeeded', '{"licenseId": 1, "type": "initial"}', 'tenant_local'),
('PAY-2025-002', 150.00, 'USD', 'Succeeded', '{"licenseId": 2, "type": "initial"}', 'tenant_local'),
('PAY-2025-003', 200.00, 'USD', 'Pending', '{"licenseId": 3, "type": "initial"}', 'tenant_local');

-- Insert sample notifications
INSERT INTO Notifications (ToAddresses, Subject, Body, Status, Metadata) VALUES
('["john.doe@example.com"]', 'License Approved', 'Your business license has been approved.', 'Sent', '{"licenseId": 1}'),
('["jane.smith@example.com"]', 'License Expiring Soon', 'Your license will expire in 6 months.', 'Sent', '{"licenseId": 2}'),
('["bob.johnson@example.com"]', 'Application Received', 'We have received your license application.', 'Sent', '{"licenseId": 3}');

GO

PRINT 'Seed data inserted successfully.';
PRINT 'Test credentials: admin@local / Password123!';
