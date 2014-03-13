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


namespace NuGetGallery.Operations
{
    [Command("CreateDatabaseReportTask", "Creates database report task", AltName = "cdrt")]
    public class CreateDatabaseReportTask : DatabaseAndStorageTask
    {
        private string SqlQueryForConnectionCount = @"select count(*) from sys.dm_exec_connections";
        private string SqlQueryForRequestCount = @"select count(*) from sys.dm_exec_requests";


        public override void ExecuteCommand()
        {
            AppendHourlyCount(SqlQueryForConnectionCount, "DBConnections");
            AppendHourlyCount(SqlQueryForRequestCount, "DBRequests");
            CreateReportForDBCPUUsage();
        }

        private void AppendHourlyCount(string sqlQuery,string blobName)
        {
            List<Tuple<string, string>> connectionCountDataPoints = new List<Tuple<string, string>>();
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var connectionCount = dbExecutor.Query<Int32>(sqlQuery).SingleOrDefault();
                    ReportHelpers.AppendDatatoBlob(StorageAccount, blobName + ".json", new Tuple<string, string>(String.Format("{0:HH:mm}", DateTime.Now), connectionCount.ToString()),5, ContainerName);

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
                ReportHelpers.CreateBlob(StorageAccount, "DBCPUTime" + ".json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
            }
        }
    }
}


