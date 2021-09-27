using System;

namespace Sample.Api.Settings
{
    public class JwtSettings
    {
        public string SecurityKey { get; set; }

        public TimeSpan ExpiresIn { get; set; }
    }
}