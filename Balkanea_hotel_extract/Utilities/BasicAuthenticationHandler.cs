using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace Balkanea_hotel_extract.Utilities
{
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        IConfiguration configuration = (new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build());

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.Fail("Missing Authorization Header");

            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
                byte[] credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                string decodedString = Encoding.UTF8.GetString(credentialBytes);
                var credentials = decodedString.Split(':');
                var username = credentials[0];
                var password = credentials[1];

                if (!ValidateCredentials(username, password))
                {
                    return AuthenticateResult.Fail("Invalid Username or Password");
                }

                var claims = new[] {
                new Claim(ClaimTypes.Name, username)
            };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch
            {
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }
        }

        private bool ValidateCredentials(string username, string password)
        {
            var test = configuration.GetSection("AllowedSettings:BasicAuth:Username").Value;
            return username == configuration.GetSection("AllowedSettings:BasicAuth:Username").Value && password == configuration.GetSection("AllowedSettings:BasicAuth:Password").Value;
        }
    }
}
