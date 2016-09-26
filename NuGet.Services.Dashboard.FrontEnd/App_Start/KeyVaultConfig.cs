﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Configuration;
using System.Web;
using NuGet.Services.Dashboard.Common;

[assembly: PreApplicationStartMethod(typeof(NuGetDashboard.KeyVaultConfig), "PreAppStart")]
namespace NuGetDashboard
{
    public static class KeyVaultConfig
    {
        public static void PreAppStart()
        {
            var configurationProcessor = new ConfigurationProcessor(
                new SecretReaderFactory(ConfigurationManager.AppSettings));

            configurationProcessor.InjectSecretsInto(ConfigurationManager.AppSettings);
        }
    }
}