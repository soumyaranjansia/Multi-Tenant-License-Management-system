var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Add HttpClient for API calls
builder.Services.AddHttpClient("Gov2BizAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add session support for JWT token storage
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register HTTP context accessor
builder.Services.AddHttpContextAccessor();

// Register API client service
builder.Services.AddScoped<IApiClient, ApiClient>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Enable session middleware

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

// API Client Interface and Implementation
public interface IApiClient
{
    Task<T?> GetAsync<T>(string endpoint);
    Task<T?> PostAsync<T>(string endpoint, object data);
    Task<T?> PutAsync<T>(string endpoint, object data);
    Task<bool> DeleteAsync(string endpoint);
    Task<HttpResponseMessage> PostFileAsync(string endpoint, MultipartFormDataContent content);
    void SetAuthToken(string token);
}

public class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(IHttpClientFactory clientFactory, IHttpContextAccessor httpContextAccessor, ILogger<ApiClient> logger)
    {
        _clientFactory = clientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }    private HttpClient GetClient()
    {
        var client = _clientFactory.CreateClient("Gov2BizAPI");
        
        // Add JWT token if available
        var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
        if (!string.IsNullOrEmpty(token))
        {
            _logger.LogInformation("Adding JWT token to request (length: {Length})", token.Length);
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _logger.LogWarning("No JWT token found in session");
        }
        
        // Add X-Tenant-ID header if available
        var tenantId = _httpContextAccessor.HttpContext?.Session.GetString("TenantId");
        if (!string.IsNullOrEmpty(tenantId))
        {
            _logger.LogInformation("Adding X-Tenant-ID header: {TenantId}", tenantId);
            client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        }
        else
        {
            _logger.LogWarning("No TenantId found in session");
        }
          // Add X-User-Role header (workaround since JWT doesn't contain role claim)
        var userRoles = _httpContextAccessor.HttpContext?.Session.GetString("UserRoles");
        var userEmail = _httpContextAccessor.HttpContext?.Session.GetString("UserEmail");
        _logger.LogInformation("Session state - UserEmail: {UserEmail}, UserRoles: {UserRoles}", userEmail, userRoles ?? "NULL");
        
        if (!string.IsNullOrEmpty(userRoles))
        {
            _logger.LogInformation("Adding X-User-Role header: {UserRoles}", userRoles);
            client.DefaultRequestHeaders.Add("X-User-Role", userRoles);
        }
        else
        {
            _logger.LogWarning("No UserRoles found in session - X-User-Role header NOT added");
        }
        
        return client;
    }

    public void SetAuthToken(string token)
    {
        _httpContextAccessor.HttpContext?.Session.SetString("JwtToken", token);
    }    public async Task<T?> GetAsync<T>(string endpoint)
    {
        var client = GetClient();
        var response = await client.GetAsync(endpoint);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("API Response from {Endpoint}: {Json}", endpoint, json.Substring(0, Math.Min(500, json.Length)));
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, 
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        _logger.LogError("API request to {Endpoint} failed with status {StatusCode}", endpoint, response.StatusCode);
        return default;
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data)
    {
        var client = GetClient();
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(endpoint, content);
        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<T>(responseJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        return default;
    }

    public async Task<T?> PutAsync<T>(string endpoint, object data)
    {
        var client = GetClient();
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync(endpoint, content);
        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<T>(responseJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        return default;
    }

    public async Task<bool> DeleteAsync(string endpoint)
    {
        var client = GetClient();
        var response = await client.DeleteAsync(endpoint);
        return response.IsSuccessStatusCode;
    }

    public async Task<HttpResponseMessage> PostFileAsync(string endpoint, MultipartFormDataContent content)
    {
        var client = GetClient();
        return await client.PostAsync(endpoint, content);
    }
}
