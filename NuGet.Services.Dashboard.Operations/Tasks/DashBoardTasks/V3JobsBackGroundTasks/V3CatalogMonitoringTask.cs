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
using AnglicanGeek.DbExecutor;
using Elmah;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using NuGetGallery.Operations.Common;
using NuGet.Services.Metadata.Catalog;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks.V3JobsBackGroundTasks
{
    [Command("V3CatalogMonitoringTask", "Checks the lag between V3 Catalog and v2 DB.Also checks that all new packages in V2 DB is present in V3 catalog", AltName = "v3cmt")]
    public class V3CatalogtMonitoringTask : DatabaseAndStorageTask
    {
        [Option("CatalogRootUrl", AltName = "cru")]
        public string CatalogRootUrl { get; set; }

        [Option("CatalogStorageAccount", AltName = "csa")]
        public string CatalogStorageAccount { get; set; }
        
        private AlertThresholds thresholdValues;
        public override void ExecuteCommand()
        {
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));         
            CheckLagBetweenDBAndCatalog();
            DoIntegrityCheckBetweenDBAndCatalog();
        }

        public void CheckLagBetweenDBAndCatalog()
        {
           
            DateTime lastDBTimeStamp = GetLastCreatedOrEditedActivityTimeFromDB();
            //Time from DB will already be in universal time. So convert catalog to universal time.
            DateTime lastCatalogCommitTimeStamp = GetCommitTimeStampFromCatalog().ToUniversalTime();
            //Take allowed lag from configuration.
            double allowedLag = thresholdValues.V3CatalogCommitTimeStampLagInMinutes;
            double actualLag = lastDBTimeStamp.Subtract(lastCatalogCommitTimeStamp).TotalMinutes;
            if (actualLag > allowedLag)
            {
                new SendAlertMailTask
                   {
                       AlertSubject = string.Format("V3 Catalog lagging behind Gallery DB by {0} minutes", actualLag),
                       Details = string.Format("Last commit time stamp in V3 Catalog is {0} where as the last updated value in Gallery DB is {1}.", lastCatalogCommitTimeStamp, lastDBTimeStamp),
                       AlertName = "Packages missing in V3 Catalog",
                       Component = "V3 Catalog",
                       Level = "Error"
                   }.ExecuteCommand();
            }
        }

        public void DoIntegrityCheckBetweenDBAndCatalog()
        {
            //Use everything in UTC so that it works consistent across machines (local and Azure VMs).
            DateTime startTime = DateTime.UtcNow.AddHours(-1);
            DateTime endTime = GetLastCreatedCursorFromCatalog().ToUniversalTime();
            HashSet<PackageEntry> dbPackages = GetDBPackagesInLastHour(startTime, endTime);
            HashSet<PackageEntry> catalogPackages = GetCatalogPackagesInLastHour(startTime, endTime);
            string dbPackagesList = string.Join(",", dbPackages.Select(e => e.ToString()).ToArray());
            Console.WriteLine("List of packages from DB: {0}", dbPackagesList);
            string catalogPackagesList = string.Join(",", catalogPackages.Select(e => e.ToString()).ToArray());
            Console.WriteLine("List of packages from Catalog: {0}", catalogPackagesList);
            var missingPackages = dbPackages.Where(e => !catalogPackages.Contains(e));
            if (missingPackages != null && missingPackages.Count() > 0)
            {
                string missingPackagesList = string.Join(",", missingPackages.Select(e => e.ToString()).ToArray());
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Packages missing in V3 Catalog"),
                    Details = string.Format("One or more packages present in Gallery DB is not present in V3 catalog. The list of packages are : {0}", missingPackagesList),
                    AlertName = "Packages missing in V3 Catalog",
                    Component = "V3 Catalog",
                    Level = "Error"
                }.ExecuteCommand();
            }
        }

        #region Private
        private DateTime GetLastCreatedOrEditedActivityTimeFromDB()
        {
            string sqlLastUpdated = "select Top(1) [LastUpdated] from [dbo].[Packages] order by [LastUpdated] desc";
            SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString);
            connection.Open();
            SqlCommand command = new SqlCommand(sqlLastUpdated, connection);
            SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);
            if (reader != null)
            {
                while (reader.Read())
                {
                    DateTime dbLastUpdatedTimeStamp = Convert.ToDateTime(reader["LastUpdated"]);
                    return dbLastUpdatedTimeStamp;
                }
            }
            return DateTime.MinValue;
        }

        private DateTime GetCommitTimeStampFromCatalog()
        {
            return GetValueFromCatalog("commitTimeStamp");
        }
       
        private DateTime GetLastCreatedCursorFromCatalog()
        {
            return GetValueFromCatalog("nuget:lastCreated");
        }
        private DateTime GetValueFromCatalog(string keyName)
        {
            WebRequest request = WebRequest.Create(CatalogRootUrl);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                DateTime catalogCommitTimeStamp = Convert.ToDateTime(objects[keyName]);
                return catalogCommitTimeStamp;
            }
        }      
        private HashSet<PackageEntry> GetDBPackagesInLastHour(DateTime startTime, DateTime endTime)
        {
            HashSet<PackageEntry> entries = new HashSet<PackageEntry>(PackageEntry.Comparer);
            string sql = string.Format(@"SELECT [Id], [NormalizedVersion]
              FROM [dbo].[PackageRegistrations] join Packages on PackageRegistrations.[Key] = Packages.[PackageRegistrationKey] where [Created] > '{0}' AND [Created] <= '{1}'", startTime.ToString("yyyy-MM-dd HH:mm:ss"), endTime.ToString("yyyy-MM-dd HH:mm:ss"));
            SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString);
            connection.Open();
            SqlCommand command = new SqlCommand(sql, connection);
            SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);
            if (reader != null)
            {
                while (reader.Read())
                {
                    string id = reader["Id"].ToString().ToLowerInvariant();
                    string version = reader["NormalizedVersion"].ToString().ToLowerInvariant();
                    entries.Add(new PackageEntry(id, version));
                }
            }
            return entries;
        }

        private HashSet<PackageEntry> GetCatalogPackagesInLastHour(DateTime startTime, DateTime endTime)
        {
            CollectorHttpClient client = new CollectorHttpClient();
            CloudStorageAccount csa = CloudStorageAccount.Parse(CatalogStorageAccount);
            var blobClient = csa.CreateCloudBlobClient();
            Uri catalogIndex = new Uri(CatalogRootUrl);
            CatalogIndexReader reader = new CatalogIndexReader(catalogIndex, client);
            var task = reader.GetEntries(); //TBD Update CatalogIndexReader to return packages based on commit time stamp.Right now it returns all packages.
            task.Wait();
            List<CatalogIndexEntry> entries = task.Result.ToList();
            //Commit time stamp is not exact reflection of creation time. But the idea is commit time stamp will be always more than created date and it will give a super set.
            //entries = entries.Where(e => e.CommitTimeStamp.ToUniversalTime() > DateTime.UtcNow.AddHours(-1)).ToList(); 
            var catalogPackages = new HashSet<PackageEntry>(entries.Select(e => new PackageEntry(e.Id, e.Version.ToNormalizedString())), PackageEntry.Comparer);
            return catalogPackages;
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
        #endregion Private
    }
}
