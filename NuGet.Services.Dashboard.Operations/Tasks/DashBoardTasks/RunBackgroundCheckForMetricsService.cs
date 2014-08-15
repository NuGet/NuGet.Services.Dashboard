using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;


namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("RunBackgroundCheckForMetricsService", "runs background check for metrics service", AltName = "rbms")]
    public class RunBackgroundCheckForMetricsService : DatabaseAndStorageTask
    {
        [Option("MetricsServiceUri", AltName = "uri")]
        public string MetricsServiceUri { get; set; }

        public const string IdKey = "id";
        public const string VersionKey = "version";
        public const string IPAddressKey = "ipAddress";
        public const string UserAgentKey = "userAgent";
        public const string OperationKey = "operation";
        public const string DependentPackageKey = "dependentPackage";
        public const string ProjectGuidsKey = "projectGuids";
        public const string HTTPPost = "POST";
        public const string MetricsDownloadEventMethod = "/DownloadEvent";
        public const string ContentTypeJson = "application/json";

        private string loggingCheckSql = @"DECLARE @lastDownloadDateTime datetime;
                                           SET @lastDownloadDateTime =
                                           (SELECT     TOP(1) [Timestamp]
                                           FROM        PackageStatistics
                                           ORDER BY    [Key] DESC)

                                           DECLARE @currentDateTime datetime;
                                           SET @currentDateTime = GETUTCDATE()

                                           DECLARE @secondsSinceLastDownload int
                                           SET @secondsSinceLastDownload = datediff(s, @lastDownloadDateTime, @currentDateTime)

                                           SELECT @secondsSinceLastDownload as secondsSinceLastDownload";
        private AlertThresholds thresholdValues = new AlertThresholds();
        public override void ExecuteCommand()
        {
            loggingStatusCheck();
            heartBeatCheck();
            
        }

        private void heartBeatCheck()
        {
            string filename = string.Format("nuget-prod-0-metrics/{0:yyyy/MM/dd/H}/",DateTime.UtcNow);
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
            CloudBlobDirectory blobdir = container.GetDirectoryReference(filename);
            string content = string.Empty;

            foreach (CloudBlockBlob blob in blobdir.ListBlobs())
            {
                if (blob != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {

                        blob.DownloadToStream(memoryStream);

                        StreamReader sr = new StreamReader(memoryStream);
                        sr.BaseStream.Seek(0, SeekOrigin.Begin);
                        string line;
                        int error = 0;
                        int total = 0;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string[] entry = line.Split(",".ToArray());

                            if (entry.Contains("Error")) error++;
                            if (entry.Contains("Information")) total++;

                        }

                        
                        int failureRate = error * 100 / (total + error);
                        if (failureRate > thresholdValues.MetricsServiceHeartbeatErrorThreshold)
                        {
                            new SendAlertMailTask
                            {
                                AlertSubject = string.Format("Error: Alert for metrics service"),
                                Details = string.Format("Metrics hear beat error is more than threshold, threshold is {0}%, current error rate is {1}%, error detail is in file {3}", thresholdValues.MetricsServiceHeartbeatErrorThreshold, failureRate, filename),
                                AlertName = string.Format("Error: Alert for metrics service"),
                                Component = "Metrics service",
                                Level = "Error"
                            }.ExecuteCommand();
                        }

                    }
                }
            }
        }

        private void loggingStatusCheck()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var request = dbExecutor.Query<Int32>(loggingCheckSql).SingleOrDefault();

                    if (request > thresholdValues.MetricsServiceStatusErrorThreshold)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Error: Alert for metrics service"),
                            Details = string.Format("Metrics logging failure happen, logging status check failed,current time is {0}, In last {1} seconds, there is no download",DateTime.Now.ToString(),request),
                            AlertName = string.Format("Error: Alert for metrics service"),
                            Component = "Metrics service",
                            Level = "Error"
                        }.ExecuteCommand();
                    }
                }
            }
        }

    }
}
