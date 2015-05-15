using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using NuGet.Services.Metadata.Catalog;


namespace NuGetGallery.Operations.Tasks.DashBoardTasks.V3JobsBackGroundTasks
{
    public class V3Utility
    {
        public static HashSet<PackageEntry> GetCatalogPackages(string catalogRootUrl, string storageConnectionString)
        {
            return GetCatalogPackages(catalogRootUrl, storageConnectionString, DateTime.MinValue, DateTime.MaxValue);
        }

        public static HashSet<PackageEntry> GetCatalogPackages(string catalogRootUrl,string storageConnectionString,DateTime startCommitTimeStamp,DateTime endCommitTimeStamp)
        {
            CollectorHttpClient client = new CollectorHttpClient();
            CloudStorageAccount csa = CloudStorageAccount.Parse(storageConnectionString);
            var blobClient = csa.CreateCloudBlobClient();
            Uri catalogIndex = new Uri(catalogRootUrl);
            CatalogIndexReader reader = new CatalogIndexReader(catalogIndex, client);
            //TBD Update CatalogIndexReader to return packages based on commit time stamp.Right now it returns all packages.
            var task = reader.GetEntries(); 
            task.Wait();
            List<CatalogIndexEntry> entries = task.Result.ToList();
            entries = entries.Where(e => e.CommitTimeStamp >= startCommitTimeStamp && e.CommitTimeStamp <= endCommitTimeStamp).ToList();
            var catalogPackages = new HashSet<PackageEntry>(entries.Select(e => new PackageEntry(e.Id, e.Version.ToNormalizedString())), PackageEntry.Comparer);
            return catalogPackages;
        }
        public static DateTime GetValueFromCatalogIndex(string catalogRootUrl, string keyName)
        {
            WebRequest request = WebRequest.Create(catalogRootUrl);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                DateTime catalogCommitTimeStamp = Convert.ToDateTime(objects[keyName]);
                return catalogCommitTimeStamp.ToUniversalTime();
            }
        }      
    }

    public class PackageEntry : IEquatable<PackageEntry>
    {
        public string Id { get; private set; }
        public string Version { get; private set; }

        public PackageEntry(string id, string version)
        {
            Id = id.ToLowerInvariant();
            Version = version.ToLowerInvariant();
        }

        public bool Equals(PackageEntry other)
        {
            return Compare(this, other);
        }

        public override string ToString()
        {
            return String.Format("{0} {1}", Id, Version);
        }

        public static bool Compare(PackageEntry x, PackageEntry y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) && StringComparer.OrdinalIgnoreCase.Equals(x.Version, y.Version);
        }

        public static IEqualityComparer<PackageEntry> Comparer
        {
            get
            {
                return new PackageEntryComparer();
            }
        }

        public class PackageEntryComparer : IEqualityComparer<PackageEntry>
        {
            public bool Equals(PackageEntry x, PackageEntry y)
            {
                return PackageEntry.Compare(x, y);
            }

            public int GetHashCode(PackageEntry obj)
            {
                return obj.ToString().GetHashCode();
            }
        }
    }
}
