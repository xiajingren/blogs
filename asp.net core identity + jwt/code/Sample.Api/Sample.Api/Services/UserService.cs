using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Sample.Api.Data;
using Sample.Api.Settings;

namespace Sample.Api.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly JwtSettings _jwtSettings;

        public UserService(UserManager<AppUser> userManager, JwtSettings jwtSettings)
        {
            _userManager = userManager;
            _jwtSettings = jwtSettings;
        }

        public async Task<TokenResult> RegisterAsync(string username, string password, string address)
        {
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                return new TokenResult()
                {
                    Errors = new[] {"user already exists!"}, //用户已存在
                };
            }

            var newUser = new AppUser() {UserName = username, Address = address};
            var isCreated = await _userManager.CreateAsync(newUser, password);
            if (!isCreated.Succeeded)
            {
                return new TokenResult()
                {
                    Errors = isCreated.Errors.Select(p => p.Description)
                };
            }

            return GenerateJwtToken(newUser);
        }

        public async Task<TokenResult> LoginAsync(string username, string password)
        {
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser == null)
            {
                return new TokenResult()
                {
                    Errors = new[] {"user does not exist!"}, //用户不存在
                };
            }

            var isCorrect = await _userManager.CheckPasswordAsync(existingUser, password);
            if (!isCorrect)
            {
                return new TokenResult()
                {
                    Errors = new[] {"wrong user name or password!"}, //用户名或密码错误
                };
            }

            return GenerateJwtToken(existingUser);
        }

        private TokenResult GenerateJwtToken(AppUser user)
        {
            var key = Encoding.ASCII.GetBytes(_jwtSettings.SecurityKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
                }),
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.Add(_jwtSettings.ExpiresIn),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var securityToken = jwtTokenHandler.CreateToken(tokenDescriptor);
            var token = jwtTokenHandler.WriteToken(securityToken);

            return new TokenResult()
            {
                AccessToken = token,
                TokenType = "Bearer"
            };
        }
    }
}