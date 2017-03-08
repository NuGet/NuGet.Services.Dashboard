// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Dashboard.Common
{
    public class ConfigurationProcessor
    {
        private ISecretReaderFactory _secretReaderFactory;

        public ConfigurationProcessor(ISecretReaderFactory secretReaderFactory)
        {
            _secretReaderFactory = secretReaderFactory;
        }

        public void InjectSecretsInto(NameValueCollection collection)
        {
            var secretReader = _secretReaderFactory.CreateSecretReader();
            var secretInjector = _secretReaderFactory.CreateSecretInjector(secretReader);

            IEnumerable keys = new List<string>(collection.AllKeys);

            foreach (string key in keys)
            {
                var framedString = collection[key];
                var newValue = secretInjector.InjectAsync(framedString).Result;
                collection.Set(key, newValue);
            }
        }
    }
}
