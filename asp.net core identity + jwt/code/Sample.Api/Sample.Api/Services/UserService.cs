using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sample.Api.Data;
using Sample.Api.Settings;

namespace Sample.Api.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly JwtSettings _jwtSettings;
        private readonly AppDbContext _appDbContext;

        public UserService(UserManager<AppUser> userManager, JwtSettings jwtSettings, AppDbContext appDbContext)
        {
            _userManager = userManager;
            _jwtSettings = jwtSettings;
            _appDbContext = appDbContext;
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

            return await GenerateJwtToken(newUser);
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

            return await GenerateJwtToken(existingUser);
        }

        public async Task<TokenResult> RefreshTokenAsync(string token, string refreshToken)
        {
            var claimsPrincipal = GetClaimsPrincipalByToken(token);
            if (claimsPrincipal == null)
            {
                // 无效的token...
                return new TokenResult()
                {
                    Errors = new[] { "1: Invalid request!" },
                };
            }

            var expiryDateUnix =
                long.Parse(claimsPrincipal.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Exp).Value);
            var expiryDateTimeUtc = UnixTimeStampToDateTime(expiryDateUnix);
            if (expiryDateTimeUtc > DateTime.UtcNow)
            {
                // token未过期...
                return new TokenResult()
                {
                    Errors = new[] { "2: Invalid request!" },
                };
            }

            var jti = claimsPrincipal.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

            var storedRefreshToken =
                await _appDbContext.RefreshTokens.SingleOrDefaultAsync(x => x.Token == refreshToken);
            if (storedRefreshToken == null)
            {
                // 无效的refresh_token...
                return new TokenResult()
                {
                    Errors = new[] { "3: Invalid request!" },
                };
            }

            if (storedRefreshToken.ExpiryTime < DateTime.UtcNow)
            {
                // refresh_token已过期...
                return new TokenResult()
                {
                    Errors = new[] { "4: Invalid request!" },
                };
            }

            if (storedRefreshToken.Invalidated)
            {
                // refresh_token已失效...
                return new TokenResult()
                {
                    Errors = new[] { "5: Invalid request!" },
                };
            }

            if (storedRefreshToken.Used)
            {
                // refresh_token已使用...
                return new TokenResult()
                {
                    Errors = new[] { "6: Invalid request!" },
                };
            }

            if (storedRefreshToken.JwtId != jti)
            {
                // refresh_token与此token不匹配...
                return new TokenResult()
                {
                    Errors = new[] { "7: Invalid request!" },
                };
            }

            storedRefreshToken.Used = true;
            //_userDbContext.RefreshTokens.Update(storedRefreshToken);
            await _appDbContext.SaveChangesAsync();

            var dbUser = await _userManager.FindByIdAsync(storedRefreshToken.UserId.ToString());
            return await GenerateJwtToken(dbUser);
        }

        private ClaimsPrincipal GetClaimsPrincipalByToken(string token)
        {
            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtSettings.SecurityKey)),
                    ClockSkew = TimeSpan.Zero,
                    ValidateLifetime = false // 不验证过期时间！！！
                };

                var jwtTokenHandler = new JwtSecurityTokenHandler();

                var claimsPrincipal =
                    jwtTokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);

                var validatedSecurityAlgorithm = validatedToken is JwtSecurityToken jwtSecurityToken
                                                 && jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                                                     StringComparison.InvariantCultureIgnoreCase);

                return validatedSecurityAlgorithm ? claimsPrincipal : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<TokenResult> GenerateJwtToken(AppUser user)
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

            var refreshToken = new RefreshToken()
            {
                JwtId = securityToken.Id,
                UserId = user.Id,
                CreationTime = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddMonths(6),
                Token = GenerateRandomNumber()
            };

            await _appDbContext.RefreshTokens.AddAsync(refreshToken);
            await _appDbContext.SaveChangesAsync();

            return new TokenResult()
            {
                AccessToken = token,
                TokenType = "Bearer",
                RefreshToken = refreshToken.Token,
                ExpiresIn = (int)_jwtSettings.ExpiresIn.TotalSeconds,
            };
        }

        private string GenerateRandomNumber(int len = 32)
        {
            var randomNumber = new byte[len];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToUniversalTime();

            return dateTimeVal;
        }

    }
}