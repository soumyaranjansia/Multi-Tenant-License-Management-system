using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Gov2Biz.LicenseService.Models.DTOs;
using Gov2Biz.LicenseService.Services;

namespace Gov2Biz.LicenseService.Controllers;

/// <summary>
/// Authentication controller for login operations.
/// Does NOT require X-Tenant-ID header (exempt from TenantMiddleware).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IJwtService jwtService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// User login endpoint.
    /// Returns JWT access token on successful authentication.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <response code="200">Returns JWT token</response>
    /// <response code="401">Invalid credentials</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for username: {Username}", request.Username);

        // Authenticate user
        var user = await _authService.AuthenticateAsync(request.Username, request.Password);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            return Unauthorized(new
            {
                error = new
                {
                    message = "Invalid username or password",
                    code = "INVALID_CREDENTIALS"
                }
            });
        }

        // Parse roles from CSV
        var roles = user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .ToArray();

        // Generate JWT token
        var token = _jwtService.GenerateToken(user.Id, user.Email, roles);

        var response = new LoginResponse
        {
            AccessToken = token,
            ExpiresIn = 3600, // 60 minutes
            TokenType = "Bearer",
            Roles = roles,
            TenantId = user.TenantId,
            UserId = user.Id,
            Email = user.Email
        };

        _logger.LogInformation(
            "User {Username} logged in successfully with roles: {Roles}, tenant: {TenantId}",
            user.Email,
            string.Join(", ", roles),
            user.TenantId);

        return Ok(response);
    }

    /// <summary>
    /// User registration endpoint.
    /// Creates a new user account and tenant if needed.
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <response code="200">Returns user details</response>
    /// <response code="400">Validation error or user already exists</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        _logger.LogInformation("Registration attempt for email: {Email}, tenant: {TenantId}", 
            request.Email, request.TenantId);

        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.TenantId) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    error = new
                    {
                        message = "TenantId, Email, and Password are required",
                        code = "INVALID_INPUT"
                    }
                });
            }

            // Register user
            var user = await _authService.RegisterUserAsync(
                request.TenantId,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Role);

            var response = new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email,
                TenantId = user.TenantId
            };

            _logger.LogInformation("User {Email} registered successfully", user.Email);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Registration failed for {Email}: {Message}", request.Email, ex.Message);
            return BadRequest(new
            {
                error = new
                {
                    message = ex.Message,
                    code = "USER_EXISTS"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", request.Email);
            return StatusCode(500, new
            {
                error = new
                {
                    message = "An error occurred during registration",
                    code = "REGISTRATION_ERROR"
                }
            });
        }
    }
}
