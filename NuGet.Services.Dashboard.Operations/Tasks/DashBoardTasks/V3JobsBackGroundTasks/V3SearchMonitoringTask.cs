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
    [Command("V3SearchMonitoringTask", "Checks the integrity of data between catalog and lucene.Also checks the last commit time stamp of the index against catalog", AltName = "v3smt")]
    public class V3SearchMonitoringTask : DatabaseAndStorageTask
    {
        [Option("SearchEndPoint", AltName = "se")]
        public string SearchEndPoint { get; set; }        
        
        [Option("CatalogRootUrl", AltName = "cru")]
        public string CatalogRootUrl { get; set; }

        [Option("CatalogStorageAccount", AltName = "csa")]
        public string CatalogStorageAccount { get; set; }

        [Option("CursorFileFullPath", AltName = "cfp")]
        public string CursorFileFullPath { get; set; }

        private AlertThresholds thresholdValues;

        public override void ExecuteCommand()
        {
            CheckLagBetweenCatalogAndLucene();
            DoIntegrityCheckBetweenCatalogAndLucene();
        }

        private void DoIntegrityCheckBetweenCatalogAndLucene()
        {
           DateTime startTime = Convert.ToDateTime(File.ReadAllText(CursorFileFullPath));
           DateTime endTime = GetLastCommitTimeStampForLucene();
           HashSet<PackageEntry> catalogPackages = V3Utility.GetCatalogPackages(CatalogRootUrl, CatalogStorageAccount, startTime,endTime);

        }

        private void CheckLagBetweenCatalogAndLucene()
        {
            DateTime lastCommitToIndex = GetLastCommitTimeStampForLucene();
            DateTime lastCommitToCatalog = V3Utility.GetValueFromCatalogIndex(CatalogRootUrl, "commitTimeStamp");
            double actualLag = lastCommitToCatalog.Subtract(lastCommitToIndex).TotalMinutes;
            double allowedLag = thresholdValues.V3SearchIndexCommitTimeStampLagInMinutes;
            if (actualLag > allowedLag)
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("V3 search lagging behind catalog by {0} minutes",actualLag),
                    Details = string.Format("The commit timestamp for V3 Search Lucene Index is {0} and the commit timestamp for Catalog is {1}", lastCommitToIndex.ToString(),lastCommitToCatalog.ToString()),
                    AlertName = "V3 Search Luence Index Lagging behind V2 DB.",
                    Component = "V3 SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            } 
        }
        private DateTime GetLastCommitTimeStampForLucene()
        {
            WebRequest request = WebRequest.Create(SearchEndPoint);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                DateTime lastCommitToIndex = Convert.ToDateTime(objects["commitUserData"]["commitTimeStamp"]);
                return lastCommitToIndex;
            }
        }

        private bool CheckIfPackageExistsInLucene(string id,string version)
        {

        }
    }
}
