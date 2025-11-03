using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GatemetricsData.ServiceLayer.Interface;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GatemetricsData.ServiceLayer.Auth
{
    // Use a simple class with a parameterless constructor so configuration binding and runtime
    // activators (e.g. Swashbuckle or other libraries) can create instances without error.
    public class JwtOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int TokenExpiryMinutes { get; set; } = 60;
    }

    public class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _opts;

        public JwtTokenService(IOptions<JwtOptions> opts) => _opts = opts.Value;

        public string GenerateToken(string username)
        {
            if (string.IsNullOrEmpty(_opts.Key)) throw new InvalidOperationException("Jwt:Key is not configured");

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(ClaimTypes.Name, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _opts.Issuer,
                audience: _opts.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_opts.TokenExpiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}