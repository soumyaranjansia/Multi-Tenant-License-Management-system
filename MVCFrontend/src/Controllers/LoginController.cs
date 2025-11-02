using Microsoft.AspNetCore.Mvc;

namespace Gov2Biz.MVCFrontend.Controllers;

public class LoginController : Controller
{
    private readonly IApiClient _apiClient;

    public LoginController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // Redirect to licenses if already logged in
        var token = HttpContext.Session.GetString("JwtToken");
        if (!string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Index", "Licenses");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        try
        {
            var response = await _apiClient.PostAsync<LoginResponse>("/auth/login", model);
            
            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                // Store JWT token and user info in session
                HttpContext.Session.SetString("JwtToken", response.AccessToken);
                HttpContext.Session.SetString("UserEmail", response.Email);
                HttpContext.Session.SetString("UserRoles", string.Join(",", response.Roles));
                HttpContext.Session.SetString("TenantId", response.TenantId);
                HttpContext.Session.SetInt32("UserId", response.UserId);
                
                _apiClient.SetAuthToken(response.AccessToken);
                
                TempData["SuccessMessage"] = "Login successful!";
                return RedirectToAction("Index", "Licenses");
            }
            
            ModelState.AddModelError("", "Invalid username or password");
            return View("Index", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Login failed: {ex.Message}");
            return View("Index", model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var response = await _apiClient.PostAsync<RegisterResponse>("/auth/register", model);
            
            if (response != null && response.UserId > 0)
            {
                TempData["SuccessMessage"] = "Registration successful! Please login.";
                return RedirectToAction("Index");
            }
            
            ModelState.AddModelError("", "Registration failed");
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Registration failed: {ex.Message}");
            return View(model);
        }
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "Logged out successfully";
        return View("Logout");
    }
}

// DTOs
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string TenantId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class RegisterResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
