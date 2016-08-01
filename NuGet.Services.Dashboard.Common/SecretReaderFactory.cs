// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Dashboard.Common
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        private string _vaultName;
        private string _clientId;
        private string _certificateThumbprint;
        private string _storeName;
        private string _storeLocation;
        private bool _validateCertificate;

        public SecretReaderFactory(NameValueCollection config)
        {
            _vaultName = config["keyVault-VaultName"];
            _clientId = config["keyVault-ClientId"];
            _certificateThumbprint = config["keyVault-CertificateThumbprint"];
            _storeName = config["keyVault-StoreName"];
            _storeLocation = config["keyVault-StoreLocation"];
            _validateCertificate = bool.Parse(config["keyVault-ValidateCertificate"]);
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
                !string.IsNullOrEmpty(_storeName) ? (StoreName)Enum.Parse(typeof(StoreName), _storeName) : StoreName.My,
                !string.IsNullOrEmpty(_storeLocation) ? (StoreLocation)Enum.Parse(typeof(StoreLocation), _storeLocation) : StoreLocation.LocalMachine,
                _validateCertificate);

            return new KeyVaultReader(keyVaultConfiguration);
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
