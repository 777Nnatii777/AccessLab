using Microsoft.AspNetCore.Mvc;

namespace AccessLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase{

[HttpGet]
public IActionResult Get()
{

    return Ok(new { message = "witaj", numer = 3, version = "0.1"});


}

}