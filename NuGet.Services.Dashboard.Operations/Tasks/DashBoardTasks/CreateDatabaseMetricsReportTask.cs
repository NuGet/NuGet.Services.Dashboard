using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using AnglicanGeek.DbExecutor;
using System;
using System.Net;
using System.Web.Script.Serialization;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using Elmah;
using NuGet.Services.Dashboard.Common;


namespace NuGetGallery.Operations
{
    [Command("CreateDatabaseReportTask", "Creates database report task", AltName = "cdrt")]
    public class CreateDatabaseMetricsReportTask : DatabaseAndStorageTask
    {
        private string SqlQueryForConnectionCount = @"select count(*) from sys.dm_exec_connections";
        private string SqlQueryForRequestCount = @"select count(*) from sys.dm_exec_requests";
        private string SqlQueryForBlockedRequestCount = @"select count(*) from sys.dm_exec_requests where status = 'suspended'";
        public override void ExecuteCommand()
        {
            AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount,"Configuration.AlertThresholds.json",ContainerName));
            GetCurrentValueAndAlert(SqlQueryForConnectionCount, "DBConnections", thresholdValues.DatabaseConnectionsThreshold);
            GetCurrentValueAndAlert(SqlQueryForRequestCount, "DBRequests", thresholdValues.DatabaseRequestsThreshold);
            GetCurrentValueAndAlert(SqlQueryForBlockedRequestCount, "DBSuspendedRequests", thresholdValues.DatabaseBlockedRequestsThreshold);
            CreateReportForDBCPUUsage();
            CreateReportForRequestDetails();
        }

        private void GetCurrentValueAndAlert(string sqlQuery,string blobName,int threshold)
        {
            List<Tuple<string, string>> connectionCountDataPoints = new List<Tuple<string, string>>();
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var connectionCount = dbExecutor.Query<Int32>(sqlQuery).SingleOrDefault();
                    if(connectionCount > threshold)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("SQL Azure database alert activated for {0}", blobName),
                            Details = string.Format("Number of {0} exceeded the threshold value. Threshold value  {1}, Current value : {2}",blobName,threshold,connectionCount),                          
                            AlertName = "SQL Azure DB alert for connections/requests count",
                            Component = "SQL Azure database"
                        }.ExecuteCommand();
                    }
                   
                    ReportHelpers.AppendDatatoBlob(StorageAccount, blobName + string.Format("{0:MMdd}", DateTime.Now) + ".json", new Tuple<string, string>(String.Format("{0:HH:mm}", DateTime.Now), connectionCount.ToString()), 50, ContainerName);
                }
            }                    
        }

        private void CreateReportForDBCPUUsage()
        {
            List<Tuple<string, string>> usageDataPoints = new List<Tuple<string, string>>();
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            var currentDbName = Util.GetDbName(ConnectionString.ConnectionString);
            using (var sqlConnection = new SqlConnection(masterConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();

                    List<DateTime> lastNTimeEntries = dbExecutor.Query<DateTime>(string.Format("select distinct Top(5) time from sys.resource_usage where database_name = '{0}' order by time desc", currentDbName.ToString())).ToList();
                    foreach (DateTime time in lastNTimeEntries)
                    {
                        Console.WriteLine("Time ..................." + time.ToString());
                        var usageSeconds = dbExecutor.Query<Int32>(string.Format("select Sum(usage_in_seconds) from sys.resource_usage where time = '{0}' AND database_name = '{1}'", time.ToString(), currentDbName)).SingleOrDefault();
                        usageDataPoints.Add(new Tuple<string, string>(String.Format("{0:HH:mm}", time.ToLocalTime()), usageSeconds.ToString()));
                    }
                }
                usageDataPoints.Reverse(); //reverse it as the array returned will have latest hour as first entry.
                JArray reportObject = ReportHelpers.GetJson(usageDataPoints);
                ReportHelpers.CreateBlob(StorageAccount, "DBCPUTime" + string.Format("{0:MMdd}", DateTime.Now) + ".json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
            }
        }

        private void CreateReportForRequestDetails()
        {
            List<Tuple<string, string>> connectionCountDataPoints = new List<Tuple<string, string>>();
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var requests = dbExecutor.Query<DatabaseRequest>("SELECT t.text, r.start_time, r.status, r.command, r.wait_type, r.wait_time FROM sys.dm_exec_requests r OUTER APPLY sys.dm_exec_sql_text(sql_handle) t​");
                    var json = new JavaScriptSerializer().Serialize(requests);
                    ReportHelpers.AppendDatatoBlob(StorageAccount, "DBRequestDetails" + string.Format("{0:MMdd}", DateTime.Now) + ".json", new Tuple<string, string>(String.Format("{0:HH:mm}", DateTime.Now), json), 50, ContainerName);
                }
            }   
          
        }
    }
}


