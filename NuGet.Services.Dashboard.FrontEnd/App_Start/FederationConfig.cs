﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel;
using System.IdentityModel.Configuration;
using System.IdentityModel.Services;
using System.IdentityModel.Services.Configuration;
using System.IdentityModel.Tokens;
using System.Linq;
using System.ServiceModel.Security;
using System.Web;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;

[assembly: PreApplicationStartMethod(typeof(NuGetDashboard.FederationConfig), "PreAppStart")]
namespace NuGetDashboard
{
    public static class FederationConfig
    {
        public static void PreAppStart()
        {
            FederatedAuthentication.FederationConfigurationCreated += (sender, args) =>
            {
                // Load config
                var audienceUrls = ConfigurationManager.AppSettings["Auth.AudienceUrl"].Split('|');
                var realm = ConfigurationManager.AppSettings["Auth.AuthenticationRealm"];
                var issuer = ConfigurationManager.AppSettings["Auth.AuthenticationIssuer"];
                var thumbprint = ConfigurationManager.AppSettings["Auth.AuthenticationIssuerThumbprint"];
                
                var idconfig = new IdentityConfiguration();
                foreach (var audienceUrl in audienceUrls)
                {
                    idconfig.AudienceRestriction.AllowedAudienceUris.Add(new Uri(audienceUrl));
                }

                var registry = new ConfigurationBasedIssuerNameRegistry();
                registry.AddTrustedIssuer(thumbprint, issuer);
                idconfig.IssuerNameRegistry = registry;
                idconfig.CertificateValidationMode = X509CertificateValidationMode.None;

                var sessionTransforms = new List<CookieTransform>() {
                    new DeflateCookieTransform(),
                    new MachineKeyTransform()
                };
                idconfig.SecurityTokenHandlers.AddOrReplace(new SessionSecurityTokenHandler(sessionTransforms.AsReadOnly()));

                var wsfedconfig = new WsFederationConfiguration(issuer, realm);
                wsfedconfig.PersistentCookiesOnPassiveRedirects = true;
                wsfedconfig.PassiveRedirectEnabled = true;
                wsfedconfig.Reply = audienceUrls[0];

                args.FederationConfiguration.IdentityConfiguration = idconfig;
                args.FederationConfiguration.WsFederationConfiguration = wsfedconfig;
                args.FederationConfiguration.CookieHandler = new ChunkedCookieHandler();
            };
        }
    }
}