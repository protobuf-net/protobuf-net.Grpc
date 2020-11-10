using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Server_CS
{
    class FakeAuthHandler : AuthenticationHandler<FakeAuthenticationSchemeOptions>
    {
        public const string SchemeName = "Fake";

        public FakeAuthHandler(
        IOptionsMonitor<FakeAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claimsIdentity = new ClaimsIdentity(SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    class FakeAuthenticationSchemeOptions : AuthenticationSchemeOptions
    { }
}
