using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gov2Biz.LicenseService.Controllers;

/// <summary>
/// Health check controller.
/// Does NOT require authentication or X-Tenant-ID header.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthzController : ControllerBase
{
    private readonly ILogger<HealthzController> _logger;

    public HealthzController(ILogger<HealthzController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    /// <response code="200">Service is healthy</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        _logger.LogDebug("Health check requested");

        return Ok(new
        {
            status = "Healthy",
            service = "LicenseService",
            timestamp = DateTime.UtcNow
        });
    }
}
