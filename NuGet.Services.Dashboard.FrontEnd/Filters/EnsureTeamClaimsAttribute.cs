using System;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using System.Linq;
using System.IdentityModel.Services;
using System.Web.Security;
using System.Configuration;

namespace NuGetDashboard.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class EnsureTeamClaimsAttribute 
        : AuthorizeAttribute
    {
        private const string IdentityProviderClaimType = "http://schemas.microsoft.com/identity/claims/identityprovider";
        private const string NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var claimsIdentity = httpContext.User.Identity as ClaimsIdentity;
            if (claimsIdentity != null && httpContext.User.Identity.IsAuthenticated)
            {
                var identityProviderClaim = claimsIdentity.Claims.FirstOrDefault(c => c.Type == IdentityProviderClaimType);
                if (identityProviderClaim == null || identityProviderClaim.Value != ConfigurationManager.AppSettings["Auth.IdentityProvider"])
                {
                    return false;
                }

                var nameClaim = claimsIdentity.Claims.FirstOrDefault(c => c.Type == NameClaimType);
                if (nameClaim == null || !IsAllowedAlias(nameClaim.Value))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool IsAllowedAlias(string claimValue)
        {
            var aliases = ConfigurationManager.AppSettings["Auth.AllowAliases"].Split(';');
            return aliases.Any(a => a == claimValue);
        }

        protected override void HandleUnauthorizedRequest(System.Web.Mvc.AuthorizationContext filterContext)
        {
            // Ensure we are signed out
            var federationAuthenticationModule = filterContext.HttpContext.ApplicationInstance
                .Modules["WSFederationAuthenticationModule"] as WSFederationAuthenticationModule;
            federationAuthenticationModule.SignOut(true);

            FederatedAuthentication.SessionAuthenticationModule.SignOut();
            FormsAuthentication.SignOut();

            filterContext.Result = new ContentResult() { Content = @"
                <html>
                    <head><title>You didn't say the magic word.</title></head>

                    <body><h1>401 - Unauthorized</h1><hr /><p>You did not say the magic word.<br /><img src=""/Images/nedry.gif"" /></p></body>
                </html>
            " };
        }
    }
}
