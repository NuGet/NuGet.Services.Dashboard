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
    [Command("createpackagedownloadreport", "Creates the stats (uploads,downloads and users) for Gallery for a given month", AltName = "csmr")]
    public class CreateStatsMonthlyReport : ReportsTask
    {
        private string SqlQueryForDownloads = @" SELECT SUM(f.DownloadCount) FROM [dbo].[Fact_Download] f JOIN [dbo].[Dimension_Date] dd ON dd.[Id] = f.Dimension_Date_Id WHERE dd.[Date] >= '{0}'AND dd.[Date] <= '{1}'";
        private string SqlQueryForUploads = @"SELECT Count (*) FROM [dbo].[Packages] where [Created] >= '{0}' AND  [Created] <= '{1}'";
        private string SqlQueryForUsers = @"SELECT Count (*) FROM [dbo].[Users] where [CreatedUtc] >= '{0}' AND [CreatedUtc] <= '{1}'";
        private DateTime startingTime; //initialize start date to the NuGet initial release time.
        [Option("MonthName", AltName = "m")]
        public string Month { get; set; }

        [Option("Year", AltName = "y")]
        public int Year { get; set; }

        [Option("WarehouseConnectionString", AltName = "wrdb")]
        public string WarehouseConnectionString { get; set; }

        public override void ExecuteCommand()
        {
            CreateWeeklyStatReportFor(ConnectionString.ConnectionString, SqlQueryForUploads,"Uploads");
            CreateWeeklyStatReportFor(ConnectionString.ConnectionString, SqlQueryForUsers, "Users");
            CreateWeeklyStatReportFor(WarehouseConnectionString, SqlQueryForDownloads, "Downloads"); //uploads and users data can be data from Gallery DB whereas downloads stats will be present in warehouse.
        }

        private void CreateWeeklyStatReportFor(string connectionString,string sqlQuery,string reportName)
        {
            startingTime = new DateTime(Year, UnixTimeStampUtility.GetMonthNumber(Month), 01); //initialize to day 01 of the given month.
            DateTime monthEndTime = new DateTime(Year, UnixTimeStampUtility.GetMonthNumber(Month), UnixTimeStampUtility.GetDaysInMonth(Month));
            List<Tuple<string, string>> uploadsDataPoints = new List<Tuple<string, string>>();
            int week = 1;
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();

                    while (startingTime <= monthEndTime)
                    {
                        DateTime endTime = startingTime.AddDays(7);
                        if (endTime > monthEndTime) endTime = monthEndTime;
                        try
                        {
                            var count = dbExecutor.Query<Int32>(string.Format(sqlQuery, startingTime.ToString("yyyy-MM-dd"), endTime.ToString("yyyy-MM-dd"))).SingleOrDefault();
                            uploadsDataPoints.Add(new Tuple<string, string>("Week" + week++, count.ToString()));
                        }
                        catch (NullReferenceException)
                        {
                            uploadsDataPoints.Add(new Tuple<string, string>("Week" + week++, "0"));
                        }
                        
                        startingTime = startingTime.AddDays(7);
                    }
                }
            }
            JArray reportObject = ReportHelpers.GetJson(uploadsDataPoints);
            ReportHelpers.CreateBlob(ReportStorage, reportName + Month + "MonthlyReport.json", "dashboard", "application/json", ReportHelpers.ToStream(reportObject));
        }
    }    
}
