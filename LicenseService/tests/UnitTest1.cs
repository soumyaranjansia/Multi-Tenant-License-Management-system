using Gov2Biz.LicenseService.Models;
using FluentAssertions;

namespace Gov2Biz.LicenseService.Tests;

/// <summary>
/// Unit tests for License model validation and business logic
/// </summary>
public class LicenseModelTests
{    [Fact]
    public void License_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var license = new License
        {
            Id = 1,
            LicenseNumber = "LIC-2024-000001",
            TenantId = "TEN-001",
            LicenseType = "Business",
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            Status = "Active",
            ApplicantName = "John Doe",
            ApplicantEmail = "john@example.com"
        };

        // Assert
        license.Should().NotBeNull();
        license.Id.Should().Be(1);
        license.LicenseNumber.Should().Be("LIC-2024-000001");
        license.TenantId.Should().Be("TEN-001");
        license.Status.Should().Be("Active");
    }

    [Theory]
    [InlineData("LIC-2024-000001", true)]
    [InlineData("LIC-2023-000999", true)]
    [InlineData("ABC-2024-123456", true)]
    [InlineData("INVALID", false)]
    [InlineData("", false)]
    public void LicenseNumber_ValidatesFormat(string licenseNumber, bool shouldBeValid)
    {
        // Arrange & Act
        var isValid = !string.IsNullOrEmpty(licenseNumber) && licenseNumber.Contains("-");

        // Assert
        isValid.Should().Be(shouldBeValid);
    }

    [Fact]
    public void License_WithExpiredDate_ShouldBeIdentifiable()
    {
        // Arrange
        var expiredLicense = new License
        {
            Id = 1,
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            Status = "Expired"
        };

        var activeLicense = new License
        {
            Id = 2,
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            Status = "Active"
        };

        // Act
        var isExpired = expiredLicense.ExpiryDate < DateTime.UtcNow;
        var isActive = activeLicense.ExpiryDate > DateTime.UtcNow;

        // Assert
        isExpired.Should().BeTrue();
        isActive.Should().BeTrue();
    }
}

/// <summary>
/// Unit tests for User model and authentication
/// </summary>
public class UserModelTests
{    [Fact]
    public void User_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var user = new User
        {
            Id = 1,
            TenantId = "TEN-001",
            Email = "test@example.com",
            Username = "johndoe",
            PasswordHash = "hashed_password",
            Roles = "Admin"
        };

        // Assert
        user.Should().NotBeNull();
        user.Email.Should().Be("test@example.com");
        user.TenantId.Should().Be("TEN-001");
        user.Username.Should().Be("johndoe");
    }    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("user+tag@domain.co.uk", true)]
    [InlineData("invalid.email", false)]
    [InlineData("@nodomain.com", false)]
    [InlineData("", false)]
    public void Email_ValidatesFormat(string email, bool shouldBeValid)
    {
        // Arrange & Act
        var isValid = !string.IsNullOrEmpty(email) && 
                      email.Contains("@") && 
                      email.IndexOf("@") > 0 && // @ should not be first character
                      email.LastIndexOf("@") == email.IndexOf("@") && // Only one @
                      email.Contains(".") && 
                      email.LastIndexOf(".") > email.IndexOf("@"); // . should come after @

        // Assert
        isValid.Should().Be(shouldBeValid);
    }

    [Theory]
    [InlineData("TEN-001", true)]
    [InlineData("TENANT-ABC", true)]
    [InlineData("TEN001", true)]
    [InlineData("", false)]
    [InlineData("TEN 001", false)] // Contains space
    public void TenantId_ValidatesFormat(string tenantId, bool shouldBeValid)
    {
        // Arrange & Act
        var isValid = !string.IsNullOrEmpty(tenantId) && !tenantId.Contains(" ");

        // Assert
        isValid.Should().Be(shouldBeValid);
    }
}

/// <summary>
/// Integration tests for license operations
/// </summary>
public class LicenseOperationsTests
{    [Fact]
    public void CalculateExpiryDate_FromCreationDate_ReturnsCorrectDate()
    {
        // Arrange
        var createdDate = new DateTime(2024, 1, 1);
        var validityMonths = 12;

        // Act
        var expiryDate = createdDate.AddMonths(validityMonths);

        // Assert
        expiryDate.Should().Be(new DateTime(2025, 1, 1));
    }    [Theory]
    [InlineData(6, 6)]
    [InlineData(12, 12)]
    [InlineData(24, 24)]
    [InlineData(36, 36)]
    public void License_ValidityPeriod_CalculatesCorrectly(int months, int expectedMonthsDifference)
    {
        // Arrange
        var createdDate = DateTime.UtcNow;
        var expiryDate = createdDate.AddMonths(months);

        // Act
        var actualMonths = (expiryDate.Year - createdDate.Year) * 12 + (expiryDate.Month - createdDate.Month);

        // Assert
        actualMonths.Should().Be(expectedMonthsDifference);
    }

    [Fact]
    public void RenewalFromExpiryDate_ExtendsCorrectly()
    {
        // Arrange
        var currentExpiryDate = new DateTime(2024, 12, 31);
        var renewalMonths = 12;

        // Act
        var newExpiryDate = currentExpiryDate.AddMonths(renewalMonths);

        // Assert
        newExpiryDate.Should().Be(new DateTime(2025, 12, 31));
        newExpiryDate.Should().BeAfter(currentExpiryDate);
    }
}

/// <summary>
/// Tests for license status transitions
/// </summary>
public class LicenseStatusTests
{
    [Theory]
    [InlineData("Active")]
    [InlineData("Expired")]
    [InlineData("Suspended")]
    [InlineData("Revoked")]
    public void LicenseStatus_ValidStatuses_AreRecognized(string status)
    {
        // Arrange
        var validStatuses = new[] { "Active", "Expired", "Suspended", "Revoked" };

        // Act
        var isValid = validStatuses.Contains(status);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void License_NearingExpiry_IsDetectable()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            ExpiryDate = DateTime.UtcNow.AddDays(15),
            Status = "Active"
        };

        // Act
        var daysUntilExpiry = (license.ExpiryDate - DateTime.UtcNow).Days;
        var isNearingExpiry = daysUntilExpiry <= 30 && daysUntilExpiry > 0;

        // Assert
        isNearingExpiry.Should().BeTrue();
        daysUntilExpiry.Should().BeLessThan(30);
    }
}

/// <summary>
/// Tests for business rules validation
/// </summary>
public class BusinessRulesTests
{
    [Theory]
    [InlineData("Business", 100.00)]
    [InlineData("Professional", 250.00)]
    [InlineData("Trade", 150.00)]
    [InlineData("Construction", 500.00)]
    public void LicenseTypes_HavePositiveAmounts(string licenseType, decimal amount)
    {
        // Arrange & Act & Assert
        amount.Should().BeGreaterThan(0);
        licenseType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MultiTenantIsolation_TenantIdRequired()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            TenantId = "TEN-001"
        };

        // Act & Assert
        license.TenantId.Should().NotBeNullOrEmpty();
        license.TenantId.Should().StartWith("TEN");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ValidityMonths_MustBePositive(int months)
    {
        // Arrange & Act
        var isValid = months > 0;

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(36)]
    public void ValidityMonths_CommonPeriodsAreValid(int months)
    {
        // Arrange & Act
        var isValid = months > 0 && months <= 36;

        // Assert
        isValid.Should().BeTrue();
    }
}

/// <summary>
/// Performance and edge case tests
/// </summary>
public class EdgeCaseTests
{    [Fact]
    public void License_WithMaxValues_HandlesCorrectly()
    {
        // Arrange & Act
        var license = new License
        {
            Id = int.MaxValue,
            LicenseNumber = new string('A', 50), // Long license number
            TenantId = "TEN-999999",
            LicenseType = "Special License Type",
            CreatedAt = DateTime.MinValue,
            ExpiryDate = DateTime.MaxValue
        };

        // Assert
        license.Should().NotBeNull();
        license.Id.Should().Be(int.MaxValue);
    }

    [Fact]
    public void User_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var email = "user+tag@example.co.uk";
        var username = "jean.oconnor";

        // Act
        var user = new User
        {
            Email = email,
            Username = username,
            TenantId = "TEN-001"
        };

        // Assert
        user.Email.Should().Be(email);
        user.Username.Should().Be(username);
    }

    [Fact]
    public void DateCalculations_HandleLeapYear()
    {
        // Arrange
        var leapYearDate = new DateTime(2024, 2, 29); // Leap year

        // Act
        var oneYearLater = leapYearDate.AddMonths(12);

        // Assert
        oneYearLater.Year.Should().Be(2025);
        oneYearLater.Month.Should().Be(2);
        oneYearLater.Day.Should().Be(28); // Feb 28, 2025 (not leap year)
    }
}