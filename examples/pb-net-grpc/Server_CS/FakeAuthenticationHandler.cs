using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Server_CS
{
    class FakeAuthHandler : AuthenticationHandler<FakeAuthOptions>
    {
        public const string SchemeName = "Fake";

        public FakeAuthHandler(
        IOptionsMonitor<FakeAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
#pragma warning disable CS0618 // Type or member is obsolete (ISystemClock vs TimeProvider)
        ISystemClock clock)
        : base(options, logger, encoder, clock)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Options.AlwaysAuthenticate)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claimsIdentity = new ClaimsIdentity(SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    class FakeAuthOptions : AuthenticationSchemeOptions
    {
        public bool AlwaysAuthenticate { get; set; } = false;
    }
}
