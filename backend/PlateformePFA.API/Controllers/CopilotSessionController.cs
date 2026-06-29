using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PlateformePFA.API.Controllers
{
    [ApiController]
    [Route("api/copilot/session")]
    [Authorize(Roles = "Admin,Responsable")]
    public class CopilotSessionController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public CopilotSessionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult CreateSession()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var nomComplet = User.FindFirst("NomComplet")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User identity not found" });
            }

            var secretKey = _configuration["JWT_SECRET"] ?? _configuration["Jwt:Key"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role ?? ""),
                new Claim(ClaimTypes.Email, email ?? ""),
                new Claim("NomComplet", nomComplet ?? "")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"],
                audience: _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            Response.Cookies.Append("pfa_copilot_session", tokenString, new CookieOptions
            {
                HttpOnly = true,
                // Secure follows the actual request scheme (honors nginx's
                // X-Forwarded-Proto via UseForwardedHeaders). A Secure cookie
                // over plain HTTP would never be sent back by the browser, so
                // keying this off the environment name silently breaks Copilot
                // in the documented HTTP-behind-nginx deployment.
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/api/copilotkit",
                MaxAge = TimeSpan.FromMinutes(15)
            });

            return Ok(new { message = "Session established" });
        }

        [HttpDelete]
        public IActionResult DeleteSession()
        {
            Response.Cookies.Append("pfa_copilot_session", "", new CookieOptions
            {
                HttpOnly = true,
                // Secure follows the actual request scheme (honors nginx's
                // X-Forwarded-Proto via UseForwardedHeaders). A Secure cookie
                // over plain HTTP would never be sent back by the browser, so
                // keying this off the environment name silently breaks Copilot
                // in the documented HTTP-behind-nginx deployment.
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/api/copilotkit",
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            });

            return Ok(new { message = "Session cleared" });
        }
    }
}
