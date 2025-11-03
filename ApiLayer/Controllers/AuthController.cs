using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GatemetricsDashboard.ServiceLayer.Auth;
using GatemetricsData.ServiceLayer.Interface;

namespace GatemetricsDashboard.ApiLayer.Controllers
    {
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public AuthController(ITokenService tokenService, IConfiguration configuration)
        {
            _tokenService = tokenService;
            _configuration = configuration;
        }

        // Public token endpoint
        [AllowAnonymous]
        [HttpPost("token")]
        public IActionResult Token([FromBody] LoginRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest();

            var configuredUser = _configuration["Auth:Username"] ?? "testuser";
            var configuredPass = _configuration["Auth:Password"] ?? "password";

            if (request.Username != configuredUser || request.Password != configuredPass)
                return Unauthorized();

            var token = _tokenService.GenerateToken(request.Username);
            return Ok(new { token });
        }

        public record LoginRequest(string Username, string Password);
    }
}