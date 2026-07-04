using Microsoft.AspNetCore.Mvc;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Healthy");
}
