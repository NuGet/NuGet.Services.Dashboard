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
    [Command("CreateDatabaseDetailedReportTask", "Creates database detailed troubleshooting report task", AltName = "cddrt")]
    public class CreateDatabaseDetailedReportTask : DatabaseAndStorageTask
    {
        [Option("LastNHours", AltName = "n")]
        public int LastNHours { get; set; }
        public override void ExecuteCommand()
        {            
            CreateReportForDataBaseEvents();
        }    

        private void CreateReportForDataBaseEvents()
        {           
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            var currentDbName = Util.GetDbName(ConnectionString.ConnectionString);
            using (var sqlConnection = new SqlConnection(masterConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();                
                       
                        var usageSeconds = dbExecutor.Query<DatabaseEvent>(string.Format("select start_time, end_time,event_type,event_count,description from sys.event_log where start_time>='{0}' and start_time<='{1}' and database_name = '{2}' and severity = 2", DateTime.UtcNow.AddHours(-LastNHours).ToString("yyyy-MM-dd hh:mm:ss"),DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss"), currentDbName));
                        var json = new JavaScriptSerializer().Serialize(usageSeconds);
                        ReportHelpers.CreateBlob(StorageAccount, "DBDetailed" + LastNHours.ToString() +  "Hour.json", ContainerName, "application/json", ReportHelpers.ToStream(json));

                        var throttlingEventCount = dbExecutor.Query<Int32>(string.Format("select count(*) from sys.event_log where start_time>='{0}' and start_time<='{1}' and database_name = '{2}' and (event_type Like 'throttling%' or event_type Like 'deadlock')", DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd hh:mm:ss"),DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss"),currentDbName)).SingleOrDefault();
                        if(throttlingEventCount > 0 && LastNHours == 1)
                        {
                            new SendAlertMailTask
                            {
                                AlertSubject = "SQL Azure DB alert activated for throttling/deadlock event",
                                Details = string.Format("Number of events exceeded threshold for DB throttling/deadlock events. Threshold count : {0}, events noticed in last hour : {1}",1,throttlingEventCount),                               
                                AlertName = "SQL Azure DB throttling/deadlock event",
                                Component = "SQL Azure Database"
                            }.ExecuteCommand();
                        }
                }               
               
            }
        }
    }
}


