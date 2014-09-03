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

namespace NuGetGallery.Operations
{
    [Command("createstatshourlyreport", "Creates the stats (uploads,downloads and users) hoourly report for Gallery ", AltName = "cshr")]
    public class CreateStatsHourlyReportTask : DatabaseAndStorageTask
    {        
        private string SqlQueryForUploads = @"SELECT Count (*) FROM [dbo].[Packages] where [Created] >= '{0}' AND  [Created] <= '{1}'";
        private string SqlQueryForUniqueUploads = @"SELECT COUNT(*)
                                                    From
                                                    (
                                                    SELECT Id
                                                    FROM [dbo].Packages 
                                                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                                                    GROUP BY PackageRegistrations.Id
                                                    HAVING MIN([Created]) >= '{0}' AND MIN([Created]) <= '{1}') data";

        private string SqlQueryForUsers = @"SELECT Count (*) FROM [dbo].[Users] where [CreatedUtc] >= '{0}' AND [CreatedUtc] <= '{1}'";
        private DateTime startingTime; //initialize start date to the NuGet initial release time.
      
        public override void ExecuteCommand()
        {
            CreateWeeklyStatReportFor(ConnectionString.ConnectionString, SqlQueryForUploads, "Uploads" + string.Format("{0:MMdd}", DateTime.Now));
            CreateWeeklyStatReportFor(ConnectionString.ConnectionString, SqlQueryForUniqueUploads, "UniqueUploads" + string.Format("{0:MMdd}", DateTime.Now));
            CreateWeeklyStatReportFor(ConnectionString.ConnectionString, SqlQueryForUsers, "Users" + string.Format("{0:MMdd}", DateTime.Now));        
        }

        private void CreateWeeklyStatReportFor(string connectionString, string sqlQuery, string reportName)
        {
            startingTime = DateTime.Now.AddHours(-1).ToUniversalTime(); //initialize to day 01 of the given month.
            DateTime endTime = DateTime.Now.ToUniversalTime();    
            List<Tuple<string, string>> uploadsDataPoints = new List<Tuple<string, string>>();      
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();                   
                        try
                        {                            
                            var count = dbExecutor.Query<Int32>(string.Format(sqlQuery, startingTime.ToString("yyyy-MM-dd HH:mm:ss"), endTime.ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();                            
                            ReportHelpers.AppendDatatoBlob(StorageAccount, reportName + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH:mm}", endTime.ToLocalTime()), count.ToString()), 50, ContainerName);
                        }
                        catch (NullReferenceException)
                        {
                            uploadsDataPoints.Add(new Tuple<string, string>("0", "0"));
                        }                        
                    }
                }          
        }
    }
}

