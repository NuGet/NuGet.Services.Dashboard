// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Dashboard.Common
{
    public class SecretReaderFactory: ISecretReaderFactory
    {
        private string _vaultName;
        private string _clientId;
        private string _certificateThumbprint;

        public SecretReaderFactory(NameValueCollection config)
        {
            _vaultName = config["keyVault-VaultName"];
            _clientId = config["keyVault-ClientId"];
            _certificateThumbprint = config["keyVault-CertificateThumbprint"];
        }

        public ISecretReader CreateSecretReader()
        {
            if (string.IsNullOrEmpty(_vaultName))
            {
                return new EmptySecretReader();
            }

            var keyVaultConfiguration = new KeyVaultConfiguration(
                _vaultName,
                _clientId,
                _certificateThumbprint,
                validateCertificate: true);

            return new KeyVaultReader(keyVaultConfiguration);
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
