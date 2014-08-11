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

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("RunBackgroundCheckForMetricsService", "runs background check for metrics service", AltName = "rbms")]
    public class RunBackgroundCheckForMetricsService : DatabaseAndStorageTask
    {
        [Option("MetricsServiceUri", AltName = "uri")]
        public string MetricsServiceUri { get; set; }

        public string sql = @"SELECT count(*) FROM [dbo].[PackageStatistics] where [UserAgent] = 'NuGetDashboard' and [Timestamp] > '{0}'";

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

        public override void ExecuteCommand()
        {
            for (int i = 0; i < 10; i++)
            {
                TryHitMetricsEndPoint("RIAServices.Server", "4.2.0", "120.0.0.0", "NuGetDashboard", "DashboardTest", "None", null);
            }

            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    string test = string.Format(sql, DateTime.UtcNow.AddMinutes(-30).ToString("yyyy-MM-dd H:mm:ss"));
                    var request = dbExecutor.Query<Int32>(string.Format(sql, DateTime.UtcNow.AddMinutes(-30).ToString("yyyy-MM-dd H:mm:ss"))).SingleOrDefault();

                    int failureRate = (10 - request)*10;

                    AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
                    if (failureRate > thresholdValues.MetricsServiceErrorThreshold)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Error: Alert for metrics service"),
                            Details = string.Format("Rate of failure exceeded Error threshold.Threshold count : {0}%, the current metrics service failure rate is {1}%", thresholdValues.MetricsServiceErrorThreshold, failureRate),
                            AlertName = string.Format("Error: Alert for metrics service"),
                            Component = "Metrics service",
                            Level = "Error"
                        }.ExecuteCommand();
                    }
                }
            }
        }

        private bool TryHitMetricsEndPoint(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = GetJObject(id, version, ipAddress, userAgent, operation, dependentPackage, projectGuids);
            bool result = TryHitMetricsEndPoint(jObject);
            return result;
        }


        private bool TryHitMetricsEndPoint(JObject jObject)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var response = httpClient.PostAsync(new Uri(MetricsServiceUri + MetricsDownloadEventMethod), new StringContent(jObject.ToString(), Encoding.UTF8, ContentTypeJson)).Result;
                    //print the header 
                    Console.WriteLine("HTTP status code : {0}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.Accepted)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (HttpRequestException hre)
            {
                Console.WriteLine("Exception : {0}", hre.Message);
                return false;
            }
        }

        private JObject GetJObject(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = new JObject();
            jObject.Add(IdKey, id);
            jObject.Add(VersionKey, version);
            if (!String.IsNullOrEmpty(ipAddress)) jObject.Add(IPAddressKey, ipAddress);
            if (!String.IsNullOrEmpty(userAgent)) jObject.Add(UserAgentKey, userAgent);
            if (!String.IsNullOrEmpty(operation)) jObject.Add(OperationKey, operation);
            if (!String.IsNullOrEmpty(dependentPackage)) jObject.Add(DependentPackageKey, dependentPackage);
            if (!String.IsNullOrEmpty(projectGuids)) jObject.Add(ProjectGuidsKey, projectGuids);


            return jObject;
        }
    }
}
