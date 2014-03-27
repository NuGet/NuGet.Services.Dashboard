using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations
{
    public static class Util
    {
        public static string GetDbName(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.InitialCatalog;
        }
        public static string GetMasterConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
            return connectionStringBuilder.ToString();
        }      
    }
}
