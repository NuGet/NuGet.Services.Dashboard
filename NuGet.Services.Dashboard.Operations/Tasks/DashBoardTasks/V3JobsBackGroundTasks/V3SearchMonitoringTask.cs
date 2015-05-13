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
    [Command("V3SearchMonitoringTask", "Checks the lag between V3 search index and v2 DB.Also checks the last commit time stamp of the index", AltName = "v3smt")]
    public class V3SearchMonitoringTask : DatabaseAndStorageTask
    {
        [Option("SearchEndPoint", AltName = "se")]
        public string SearchEndPoint { get; set; }
        private AlertThresholds thresholdValues;

        [Option("CatalogRootUrl", AltName = "cru")]
        public string CatalogRootUrl { get; set; }


        public override void ExecuteCommand()
        {
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            //Check last commit timestamp
            CheckLastCommitTimeStamp();
            //Check the lag between Index and DB.
            CheckLuceneIndexLag();
            DoIntegrityCheckBetweenDBAndCatalog();
        }

        private void DoIntegrityCheckBetweenDBAndCatalog()
        {
           
        }



        private void CheckLastCommitTimeStamp()
        {
            WebRequest request = WebRequest.Create(SearchEndPoint);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                string lastCommit = (string)objects["commitUserData"]["commitTimeStamp"];   
                if(!string.IsNullOrEmpty(lastCommit))
                {                 
                    DateTime lastCommitTime = Convert.ToDateTime(lastCommit);                    
                    TimeSpan delayTimeSpan = DateTime.Now.Subtract(lastCommitTime);
                    if(delayTimeSpan > new TimeSpan(0,thresholdValues.V3SearchIndexCommitTimeStampLagInMinutes,0))
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("V3 search Lucene index not updated in last {0} minutes",delayTimeSpan.TotalMinutes),                          
                            Details = string.Format("The commit timestamp for V3 Search Lucene Index is {0}. Make sure the jobs are running fine.",lastCommitTime.ToString()),
                            AlertName = "V3 Search Luence Index Lagging behind V2 DB.",
                            Component = "V3 SearchService",
                            Level = "Error"
                        }.ExecuteCommand();
                    }
                }
            }
        }

        private void CheckLuceneIndexLag()
        {
            AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            int diff = GetTotalPackageCountFromDatabase() - GetTotalPackageCountFromLucene();
            //Check for both positive and negative lag so that we will know if both add/deletes are getting applied to Lucene.
            if (diff > thresholdValues.V3LuceneIndexLagThreshold || diff < (-1) * thresholdValues.V3LuceneIndexLagThreshold)
            {
                new SendAlertMailTask
                {
                    AlertSubject = "V3 Search Luence Index Lagging behind V2 DB.",
                    Details = string.Format("There are around {0} new packages in V2 DB which are not present in V3 Lucene Index.", diff),
                    AlertName = "V3 Search Luence Index Lagging behind V2 DB.",
                    Component = "V3 SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            }
        }

        #region Private

        private DateTime GetLastCreatedCursorFromCatalog()
        {
            WebRequest request = WebRequest.Create(SearchEndPoint);      
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                DateTime catalogLastCreated = Convert.ToDateTime(objects["nuget:lastCreated"]);
                return catalogLastCreated;
            }
        }
        private HashSet<PackageEntry> GetDBPackagesInLastHour(DateTime catalogLastCreatedCursor)
        {
            HashSet<PackageEntry> entries = new HashSet<PackageEntry>(PackageEntry.Comparer);
            string sql = string.Format(@"SELECT [Id], [NormalizedVersion]
              FROM [dbo].[PackageRegistrations] join Packages on PackageRegistrations.[Key] = Packages.[PackageRegistrationKey] where [Created] > {0} AND [Created] <= {1}", DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"), catalogLastCreatedCursor.ToString("yyyy-MM-dd HH:mm:ss"));
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

        private HashSet<PackageEntry> GetCatalogPackagesInLastHour(DateTime catalogLastCreatedCursor)
        {
            TransHttpClient client = new TransHttpClient(StorageAccount, "https://api.nuget.org/");
            var blobClient = StorageAccount.CreateCloudBlobClient();
            Uri catalogIndex = new Uri(CatalogRootUrl);
            CatalogIndexReader reader = new CatalogIndexReader(catalogIndex, client);
            var task = reader.GetEntries();
            task.Wait();
            List<CatalogIndexEntry> entries = task.Result.ToList();
            var catalogPackages = new HashSet<PackageEntry>(entries.Select(e => new PackageEntry(e.Id, e.Version.ToNormalizedString())), PackageEntry.Comparer);
        }

        private int GetTotalPackageCountFromDatabase()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    //Get only listed packages as V3 lucene currently has only listed ones due to https://github.com/NuGet/NuGetGallery/issues/2475
                    var connectionCount = dbExecutor.Query<Int32>("select count(*) from dbo.Packages where Listed = 1").SingleOrDefault(); 
                    return connectionCount;
                }
            }
        }

        private int GetTotalPackageCountFromLucene()
        {
            WebRequest request = WebRequest.Create(SearchEndPoint);       
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                int count = (int)objects["numDocs"];
                return count;
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
        #endregion Private
    }
}
