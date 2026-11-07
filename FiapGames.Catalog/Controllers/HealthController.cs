using Microsoft.AspNetCore.Mvc;
using FiapGames.Catalog.Search;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("health")]
public class HealthController(SearchIndexState search) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Healthy");

    // Endpoint da demo de live deploy: trocar "demo-v1" por "demo-v2", dar push e ver a
    // resposta mudar em produção quando o rolling update no EKS terminar.
    [HttpGet("version")]
    public IActionResult Version() => Ok(new { service = "catalog", version = "demo-v1" });

    // Liveness stays independent of OpenSearch; readiness waits for the initial
    // index load, so a rolling deploy does not route traffic to an unready pod.
    [HttpGet("ready")]
    public IActionResult Ready() => search.IsReady ? Ok("Ready") : StatusCode(503, "Search index not ready");
}
