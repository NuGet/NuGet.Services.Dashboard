﻿using System.Collections.Generic;
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
using System.IO;
using NuGet.Services.Dashboard.Common;


namespace NuGetGallery.Operations
{
    [Command("RunBackgroundCheckForFailoverDC", "Checks the status of database and storage sync jobs in failover dc", AltName = "rbgfdc")]
    public class RunBackgroundCheckForFailoverDC : DatabaseAndStorageTask
    {

        [Option("PackagesStorageAccount", AltName = "pst")]
        public CloudStorageAccount PackagesStorage { get; set; }

        public string sqlQueryForDbAge = "select create_date from sys.databases where name = 'NuGetGallery'";
        public string sqlQueryForPackagesCount = "select count(*) from [dbo].[Packages]";
        public string sqlQueryForUsersCount = "select count(*) from [dbo].[Users]";
        private const string PackagesContainerName = "packages";
        public AlertThresholds thresholdValues;
        
        public override void ExecuteCommand()
        {
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            List<Tuple<string, string>> jobOutputs = new List<Tuple<string, string>>();
             jobOutputs.Add(new Tuple<string,string>("ImportDBToFailoverDC", CheckAgeOfLiveDatabase()));
             jobOutputs.Add(new Tuple<string,string>("SyncPackagesToFailoverDC", CheckLagBetweenDBAndBlob()));
             jobOutputs.Add(new Tuple<string, string>("ImportCompletionStatus", CheckForInCompleteDBImport()));
            JArray reportObject = ReportHelpers.GetJson(jobOutputs);
            ReportHelpers.CreateBlob(StorageAccount, "RunBackgroundCheckForFailoverDCReport.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));        
        }

        private string CheckAgeOfLiveDatabase()
        {
            string outputMessage;
            var cstr = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            using(var connection = new SqlConnection(cstr))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();
                var dbAge = db.Query<DateTime>(sqlQueryForDbAge).SingleOrDefault();
                double delta = DateTime.UtcNow.Subtract(dbAge).TotalMinutes;               
                outputMessage = string.Format("The NuGetGallery DB created time in failover DC as of {0} UTC is {1}. Current Lag: {2} minutes. Allowed lag: {3} minutes", DateTime.UtcNow.ToString(),dbAge.ToString(),delta ,thresholdValues.FailoverDBAgeThresholdInMinutes);
               if(delta > thresholdValues.FailoverDBAgeThresholdInMinutes)
               {
                   new SendAlertMailTask
                   {
                       AlertSubject = "Failover Datacentre alert activated for Import Database job",
                       Details = outputMessage,
                       AlertName = "Alert for ImportDatabaseToFailoverDC",
                       Component = "ImportDatabaseToFailOverDC"
                   }.ExecuteCommand();
               }
            }
            Console.WriteLine(outputMessage);
            return outputMessage;
        }

        private string CheckForInCompleteDBImport()
        {
            string outputMessage;
         
            using (var connection = new SqlConnection(ConnectionString.ConnectionString))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();
                var usersCount = db.Query<Int32>(sqlQueryForUsersCount).SingleOrDefault();            
                outputMessage = string.Format("The Failover DB doesn't seem to have imported properly. This means that the DB pointed by live site has incomplete data.Count of records in Users table : {0}. Expected : Atleast 39 K", usersCount);
                if (usersCount < 39000)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Failover Datacentre alert activated for Incomplete Import",
                        Details = outputMessage,
                        AlertName = "Alert for InCompleteDBImportInFailoverDC",
                        Component = "ImportDatabaseToFailOverDC"
                    }.ExecuteCommand();
                }
            }
            Console.WriteLine(outputMessage);
            return outputMessage;
        }


        private string CheckLagBetweenDBAndBlob()
        {
            string outputMessage;
          
            using (var connection = new SqlConnection(ConnectionString.ConnectionString))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();
                var rowCount = db.Query<int>(sqlQueryForPackagesCount).SingleOrDefault();

                CloudBlobClient blobClient =  PackagesStorage.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(PackagesContainerName);
            
                int blobCount = container.ListBlobs().Count();
                int delta = rowCount - blobCount;
                outputMessage = string.Format("The delta between packages in failover DB and blob is {0}. Threshold is {1}. This means that there are packages in DB and blob are not in sync and downloads for some packages may fail.",delta, thresholdValues.FailoverDBAndBlobLag);
                if (delta > thresholdValues.FailoverDBAndBlobLag || delta < -20)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Failover Datacentre alert activated for DB and Blob lag.",
                        Details = outputMessage,
                        AlertName = "Alert for SyncPackagesInFailOverDC",
                        Component = "SyncPackagesInFailOverDC"
                    }.ExecuteCommand();
                }
            }
            Console.WriteLine(outputMessage);
            return outputMessage;
        }

    }
}
