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
    [Command("V3SearchAndRegistrationBlobMonitoringTask", "Checks the integrity of data between catalog and lucene/registration blob.Also checks the last commit time stamp of the index against catalog", AltName = "v3srmt")]
    public class V3SearchAndRegistrationBlobMonitoringTask : DatabaseAndStorageTask
    {
        [Option("SearchEndPoint", AltName = "se")]
        public string SearchEndPoint { get; set; }        
        
        [Option("CatalogRootUrl", AltName = "cru")]
        public string CatalogRootUrl { get; set; }

        [Option("RegistrationBlobUrl", AltName = "rbu")]
        public string RegistrationBlobUrl { get; set; }

        [Option("CatalogStorageAccount", AltName = "csa")]
        public string CatalogStorageAccount { get; set; }

        [Option("CursorFileFullPath", AltName = "cfp")]
        public string CursorFileFullPath { get; set; }

        private AlertThresholds thresholdValues;

        public override void ExecuteCommand()
        {
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));                  
            CheckLagBetweenCatalogAndLucene();
            DoIntegrityCheckBetweenCatalogAndLucene();
        }
        private void CheckLagBetweenCatalogAndLucene()
        {
            DateTime lastCommitToIndex = GetLastCommitTimeStampForLucene();
            DateTime lastCommitToCatalog = V3Utility.GetValueFromCatalogIndex(CatalogRootUrl, "commitTimeStamp");
            double actualLag = lastCommitToCatalog.Subtract(lastCommitToIndex).TotalMinutes;
            //Allowed lag should be taken from config and it should be in seconds. But right now, the lag noticed is around 10 minutes. So setting it to 20
            //Until we fix the issue https://github.com/NuGet/NuGetGallery/issues/2479
            //double allowedLag = thresholdValues.V3SearchIndexCommitTimeStampLagInMinutes;
            double allowedLag = 20;
            Console.WriteLine("Lag between catalog and Lucene : {0}", actualLag);
            if (actualLag > allowedLag)
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("V3 search lagging behind catalog by {0} minutes", actualLag),
                    Details = string.Format("The commit timestamp for V3 Search Lucene Index is {0} and the commit timestamp for Catalog is {1}", lastCommitToIndex.ToString(), lastCommitToCatalog.ToString()),
                    AlertName = "V3 Search Luence Index Lagging behind V2 DB.",
                    Component = "V3 SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            }
        }
        private void DoIntegrityCheckBetweenCatalogAndLucene()
        {
           DateTime startTime = Convert.ToDateTime(File.ReadAllText(CursorFileFullPath));
           DateTime endTime = GetLastCommitTimeStampForLucene();
           HashSet<PackageEntry> catalogPackages = V3Utility.GetCatalogPackages(CatalogRootUrl, CatalogStorageAccount, startTime,endTime);
           List<PackageEntry> missingPackagesFromLucene = GetMissingPackagesFromLuceneAndRegistrationBlob(catalogPackages);
           if(missingPackagesFromLucene != null & missingPackagesFromLucene.Count > 0)
            {
                string missingPackagesString = string.Join(",", missingPackagesFromLucene.Select(e => e.ToString()).ToArray());
                Console.WriteLine("Missing Packages : {0}", missingPackagesString);
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Packages missing in V3 Lucene Index/RegistrationBlob"),
                    Details = string.Format("List of packages that are found in catalog and missing either in Lucene Index/Registration Blob: {0}", missingPackagesString),
                    AlertName = "Packages missing in V3 Lucene Index",
                    Component = "V3 SearchService/RegistrationBlob",
                    Level = "Error"
                }.ExecuteCommand();
            }
           else
           {
               //Update cursor only if validation succeeds.
               File.WriteAllText(CursorFileFullPath, endTime.ToString());
           }            
        }
             
        private DateTime GetLastCommitTimeStampForLucene()
        {
            WebRequest request = WebRequest.Create(string.Format("{0}/stats",SearchEndPoint));
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                DateTime lastCommitToIndex = Convert.ToDateTime(objects["commitUserData"]["commitTimeStamp"]);
                return lastCommitToIndex.ToUniversalTime();
            }
        }

        private List<PackageEntry> GetMissingPackagesFromLuceneAndRegistrationBlob(HashSet<PackageEntry> catalogPackages)
        {
            List<PackageEntry> missingPackagesFromLucene = new List<PackageEntry>();
            foreach (PackageEntry Catalogentry in catalogPackages)
            {
                try
                {
                    WebRequest request = WebRequest.Create(string.Format("{0}/find?id={1}&version={2}", SearchEndPoint, Catalogentry.Id, Catalogentry.Version));
                    request.PreAuthenticate = true;
                    request.Method = "GET";
                    WebResponse respose = request.GetResponse();
                    var statusCode = ((HttpWebResponse)respose).StatusCode;
                    if (statusCode != HttpStatusCode.OK)
                    {
                        missingPackagesFromLucene.Add(new PackageEntry(Catalogentry.Id, Catalogentry.Version));
                    }
                    //Check if registration blob exists as well for that Id and version.
                    request = WebRequest.Create(string.Format("{0}/{1}/{2}.json", RegistrationBlobUrl, Catalogentry.Id.ToLowerInvariant(), Catalogentry.Version.ToLowerInvariant()));
                    request.PreAuthenticate = true;
                    request.Method = "GET";
                    respose = request.GetResponse();
                    statusCode = ((HttpWebResponse)respose).StatusCode;
                    if (statusCode != HttpStatusCode.OK)
                    {
                        missingPackagesFromLucene.Add(new PackageEntry(Catalogentry.Id, Catalogentry.Version));
                    }
                }catch(Exception)
                {   //Add it to missing packages even if the server returns 503.
                    missingPackagesFromLucene.Add(new PackageEntry(Catalogentry.Id, Catalogentry.Version));
                }

            }
            return missingPackagesFromLucene;
        }
    }
}
