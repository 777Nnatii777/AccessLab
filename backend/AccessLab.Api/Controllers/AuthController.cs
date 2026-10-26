using AccessLab.Application.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace AccessLab.Api.Controllers;

[ApiController]
[Route("api/dev/auth")]
public class AuthController : ControllerBase
{
    private readonly ILoginService _loginService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        ILoginService loginService,
        IWebHostEnvironment environment)
    {
        _loginService = loginService;
        _environment = environment;
    }

    [HttpPost("check-credentials")]
    public async Task<IActionResult> CheckCredentials(
        [FromBody] LoginRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _loginService.CheckCredentialsAsync(request);

        if (result.Status == LoginCheckResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Login lub hasło niepoprawne"
            });
        }


        return Ok(new
        {
            status = result.Status.ToString()
        });
    }
}