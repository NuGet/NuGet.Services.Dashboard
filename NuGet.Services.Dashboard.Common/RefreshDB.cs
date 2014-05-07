using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;


namespace NuGet.Services.Dashboard.Common
{
    public class RefreshDB
    {
        private string ConnectionString { get; set; }

        private int LastNHours { get; set; }

        public RefreshDB(string ConnectionString, int LastNHours)
        {
            this.ConnectionString = ConnectionString;
            this.LastNHours = LastNHours;
        }

        public List<DatabaseEvent> RefreshDatabaseEvent() 
        {
            var masterConnectionString = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ToString();
            var currentDbName = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            using (var sqlConnection = new SqlConnection(masterConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();

                    var usageSeconds = dbExecutor.Query<DatabaseEvent>(string.Format("select start_time, end_time,event_type,event_count,description from sys.event_log where start_time>='{0}' and start_time<='{1}' and database_name = '{2}' and severity = 2", DateTime.UtcNow.AddHours(-LastNHours).ToString("yyyy-MM-dd hh:mm:ss"), DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss"), currentDbName));
                    return usageSeconds.ToList();
                }

            }
        }

        public List<DatabaseRequest> RefreshDatebaseRequest() 
        {
            List<Tuple<string, string>> connectionCountDataPoints = new List<Tuple<string, string>>();
            using (var sqlConnection = new SqlConnection(ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var requests = dbExecutor.Query<DatabaseRequest>("SELECT t.text, r.start_time, r.status, r.command, r.wait_type, r.wait_time FROM sys.dm_exec_requests r OUTER APPLY sys.dm_exec_sql_text(sql_handle) t​");
                    return requests.ToList();
                }
            }   
        }
    }
}
