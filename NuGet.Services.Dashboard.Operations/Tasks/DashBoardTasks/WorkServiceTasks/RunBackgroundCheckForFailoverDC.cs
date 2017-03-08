using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Script.Serialization;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;


namespace NuGetGallery.Operations
{
    [Command("RunBackgroundCheckForFailoverDC", "Checks the status of database and storage sync jobs in failover dc", AltName = "rbgfdc")]
    public class RunBackgroundCheckForFailoverDC : DatabaseAndStorageTask
    {

        [Option("PackagesStorageAccount", AltName = "pst")]
        public CloudStorageAccount PackagesStorage { get; set; }

        [Option("DbName", AltName = "dbn")]
        public string DbName { get; set; }

        public string sqlQueryForDbAge;
        public string sqlQueryForPackagesCount = "select count(*) from [dbo].[Packages]";
        public string sqlQueryForUsersCount = "select count(*) from [dbo].[Users]";
        private const string PackagesContainerName = "packages";
        public AlertThresholds thresholdValues;
        
        public override void ExecuteCommand()
        {
            sqlQueryForDbAge = string.Format("select create_date from sys.databases where name = '{0}'", DbName);
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            List<Tuple<string, string>> jobOutputs = new List<Tuple<string, string>>();
            jobOutputs.Add(new Tuple<string,string>("SyncPackagesToFailoverDC", CheckLagBetweenDBAndBlob()));
            jobOutputs.Add(new Tuple<string, string>("ImportCompletionStatus", CheckForInCompleteDBImport()));
            JArray reportObject = ReportHelpers.GetJson(jobOutputs);
            ReportHelpers.CreateBlob(StorageAccount, "RunBackgroundCheckForFailoverDCReport.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));        
        }


        private string CheckForInCompleteDBImport()
        {
            string outputMessage;
         
            using (var connection = new SqlConnection(ConnectionString.ConnectionString))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();
                var usersCount = db.Query<Int32>(sqlQueryForUsersCount).SingleOrDefault();            
                outputMessage = string.Format("The Failover DB doesn't seem to have imported properly. This means that the DB pointed by live site has incomplete data.Count of records in Users table : {0}. Error Expected : Atleast {1}", usersCount,thresholdValues.DatabaseImportErrorThreshold);
                if (usersCount < thresholdValues.DatabaseImportErrorThreshold)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Error: Failover Datacentre alert activated for Incomplete Import",
                        Details = outputMessage,
                        AlertName = "Error: Alert for InCompleteDBImportInFailoverDC",
                        Component = "ImportDatabaseToFailOverDC",
                        Level = "Error"
                    }.ExecuteCommand();
                }
                else if (usersCount < thresholdValues.DatabaseImportWarningThreshold)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Warning: Failover Datacentre alert activated for Incomplete Import",
                        Details =  string.Format("The Failover DB doesn't seem to have imported properly. This means that the DB pointed by live site has incomplete data.Count of records in Users table : {0}.Warning Expected : Atleast {1}", usersCount,thresholdValues.DatabaseImportWarningThreshold),
                        AlertName = "Warning: Alert for InCompleteDBImportInFailoverDC",
                        Component = "ImportDatabaseToFailOverDC",
                        Level = "Waring"
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
                outputMessage = string.Format("The delta between packages in failover DB and blob is {0}. Error Threshold is {1}. This means that there are packages in DB and blob are not in sync and downloads for some packages may fail.",delta, thresholdValues.FailoverDBAndBlobLagErrorThreshold);
                if (delta > thresholdValues.FailoverDBAndBlobLagErrorThreshold || delta < -20)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Error: Failover Datacentre alert activated for DB and Blob lag.",
                        Details = outputMessage,
                        AlertName = "Error: Alert for SyncPackagesInFailOverDC",
                        Component = "SyncPackagesInFailOverDC",
                        Level = "Error"
                    }.ExecuteCommand();
                }
                else if (delta > thresholdValues.FailoverDBAndBlobLagWarningThreshold)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Warning: Failover Datacentre alert activated for DB and Blob lag.",
                        Details = string.Format("The delta between packages in failover DB and blob is {0}. Warning Threshold is {1}. This means that there are packages in DB and blob are not in sync and downloads for some packages may fail.",delta, thresholdValues.FailoverDBAndBlobLagWarningThreshold),
                        AlertName = "Warning: Alert for SyncPackagesInFailOverDC",
                        Component = "SyncPackagesInFailOverDC",
                        Level = "Warning"
                    }.ExecuteCommand();
                }
            }
            Console.WriteLine(outputMessage);
            return outputMessage;
        }

    }
}

