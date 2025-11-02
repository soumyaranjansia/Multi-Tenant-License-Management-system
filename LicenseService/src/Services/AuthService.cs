using Microsoft.EntityFrameworkCore;
using Gov2Biz.LicenseService.Data;
using Gov2Biz.LicenseService.Models;

namespace Gov2Biz.LicenseService.Services;

/// <summary>
/// Service for user authentication operations.
/// </summary>
public interface IAuthService
{
    Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User> RegisterUserAsync(string tenantId, string email, string password, string firstName, string lastName, string role, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly LicenseDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(LicenseDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> AuthenticateAsync(
        string username, 
        string password, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Authenticating user {Username}", username);

        // Find user by username or email
        var user = await _context.Users
            .Where(u => u.IsActive && (u.Username == username || u.Email == username))
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User {Username} not found", username);
            return null;
        }

        // Verify password with BCrypt
        // For demo purposes, we'll accept "Password123!" for all users
        // In production, verify against user.PasswordHash with BCrypt.Net
        bool isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) 
            || password == "Password123!"; // Demo fallback

        if (!isValidPassword)
        {
            _logger.LogWarning("Invalid password for user {Username}", username);
            return null;
        }

        _logger.LogInformation("User {Username} authenticated successfully", username);
        return user;
    }

    public async Task<User?> GetUserByUsernameAsync(
        string username, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Where(u => u.IsActive && (u.Username == username || u.Email == username))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User> RegisterUserAsync(
        string tenantId,
        string email,
        string password,
        string firstName,
        string lastName,
        string role,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering new user: {Email} for tenant: {TenantId}", email, tenantId);

        // Check if user already exists
        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (existingUser)
        {
            _logger.LogWarning("User with email {Email} already exists", email);
            throw new InvalidOperationException($"User with email {email} already exists");
        }

        // Create tenant if it doesn't exist
        var tenantExists = await _context.Tenants
            .AnyAsync(t => t.TenantId == tenantId, cancellationToken);

        if (!tenantExists)
        {
            _logger.LogInformation("Creating new tenant: {TenantId}", tenantId);
            var tenant = new Tenant
            {
                TenantId = tenantId,
                Name = tenantId, // Use tenantId as default name
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        // Create new user (using email prefix as username)
        var user = new User
        {
            TenantId = tenantId,
            Username = email.Split('@')[0], // Use email prefix as username
            Email = email,
            PasswordHash = passwordHash,
            Roles = role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Email} registered successfully with ID: {UserId}", email, user.Id);
        
        return user;
    }
}
