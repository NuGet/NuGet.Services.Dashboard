using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("RunBackGroundChecksForWorkerJobs", "Runs background checks for the worker jobs", AltName = "rbgc")]
    public class RunBackGroundChecksForWorkerJobs : DatabaseAndStorageTask
    {
        private const string BackupPrefix = "backup_";
        private const string PackagesContainerName = "packages";
        private const string BackupPackagesContainerName = "ng-backups";
        public AlertThresholds thresholdValues;

        private string prodSQL = @"SELECT COUNT(*) AS [DownloadCount]
                                        , PackageId
                                        , PackageVersion
                                        , Operation
                                    FROM(
                                         SELECT Id AS PackageId
                                        , Version AS PackageVersion
                                        , Operation
                                    FROM        PackageStatistics
                                    INNER JOIN Packages ON Packages.[Key] = PackageStatistics.PackageKey
                                    INNER JOIN  PackageRegistrations ON PackageRegistrations.[Key] = Packages.PackageRegistrationKey
                                    WHERE Timestamp >= '{0}'
                                    AND Timestamp < '{1}'
                                    ) data
                                    GROUP BY PackageId
                                    , PackageVersion
                                    , Operation
                                    ORDER BY[DownloadCount] DESC";

        private string wareSQL = @"SELECT  SUM(DownloadCount) AS DownloadCount
                                            ,   PackageId
                                            ,   PackageVersion
                                            ,   Operation
                                            FROM        Fact_Download
                                            INNER JOIN  Dimension_Date ON Dimension_Date.Id = Dimension_Date_Id
                                            INNER JOIN  Dimension_Package ON Dimension_Package.Id = Dimension_Package_Id
                                            INNER JOIN  Dimension_Operation ON Dimension_Operation.Id = Dimension_Operation_Id
                                            WHERE       [Date] = '{0}'
                                            GROUP BY    PackageId
                                            ,   PackageVersion
                                            ,   Operation
                                            ORDER BY    DownloadCount DESC";

        [Option("Connection string to the warehouse database server", AltName = "wdb")]
        public SqlConnectionStringBuilder WarehouseDb { get; set; }

        [Option("PackagesStorageAccount", AltName = "iis")]
        public CloudStorageAccount PackagesStorage { get; set; }
        
        [Option("WorkServiceUserName", AltName = "name")]
        public string WorkServiceUserName { get; set; }

        [Option("WorkServiceAdminKey", AltName = "key")]
        public string WorkServiceAdminKey { get; set; }

        [Option("WorkServiceEndpoint", AltName = "url")]
        public string WorkServiceEndpoint { get; set; }


        public override void ExecuteCommand()
        {
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            List<Tuple<string, string>> jobOutputs = new List<Tuple<string, string>>();
            jobOutputs.Add(new Tuple<string, string>("PackageStatics", CheckoutForPackageStatics()));
            //jobOutputs.Add(new Tuple<string, string>("PurgePackageStatistics", CheckForPurgePackagStatisticsJob()));
            jobOutputs.Add(new Tuple<string, string>("HandleQueuedPackageEdits", CheckForHandleQueuedPackageEditJob()));
            // jobOutputs.Add(new Tuple<string, string>("BackupPackages", CheckForBackupPackagesJob())); commenting out this check temporarily as ListBlobs on ng-backups container is giving error.
            JArray reportObject = ReportHelpers.GetJson(jobOutputs);
            ReportHelpers.CreateBlob(StorageAccount, "RunBackGroundChecksForWorkerJobsReport.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));              
        }

        #region PrivateMethods
           

            private string CheckForPurgePackagStatisticsJob()
            {
                string outputMessage;
                using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
                {
                    using (var dbExecutor = new SqlExecutor(sqlConnection))
                    {
                        sqlConnection.Open();
                        //Get the count of records which are older than 7 days.
                        var ErrorOldRecordCount = dbExecutor.Query<Int32>(string.Format("select count(*) from dbo.PackageStatistics where TimeStamp <= '{0}'", DateTime.UtcNow.AddDays(thresholdValues.PurgeStatisticsErrorThresholdInDays * -1).ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();
                        var WarningOldRecordCount = dbExecutor.Query<Int32>(string.Format("select count(*) from dbo.PackageStatistics where TimeStamp <= '{0}'", DateTime.UtcNow.AddDays(thresholdValues.PurgeStatisticsWarningThresholdInDays * -1).ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();
                        outputMessage = string.Format("No of Old stats record found online is {0}. Acceptable Error threshold lag in no. of days: {1}", ErrorOldRecordCount, thresholdValues.PurgeStatisticsErrorThresholdInDays);
                        if(ErrorOldRecordCount > 0)
                        {
                            string urlLog = getLastInvocation("PurgeTransferredStatistics", 2);
                            new SendAlertMailTask
                            {
                                AlertSubject = "Error: Work service job background check alert activated for PurgePackageStatistics job",
                                Details = outputMessage + Environment.NewLine + string.Format("last two log url is {0}", urlLog),
                                AlertName = "Error: Alert for PurgePackageStatistics",
                                Component = "PurgePackageStatistics Job",
                                Level = "Error"
                            }.ExecuteCommand();
                        }
                        else if (WarningOldRecordCount > 0)
                        {
                            string urlLog = getLastInvocation("PurgeTransferredStatistics", 2);
                            new SendAlertMailTask
                            {
                                AlertSubject = "Warning: Work service job background check alert activated for PurgePackageStatistics job",
                                Details = string.Format("No of Old stats record found online is {0}. Acceptable Warning threshold lag in no. of days: {1}, last two log url is {2}", ErrorOldRecordCount, thresholdValues.PurgeStatisticsWarningThresholdInDays, urlLog),
                                AlertName = "Warning: Alert for PurgePackageStatistics",
                                Component = "PurgePackageStatistics Job",
                                Level = "Warning"
                            }.ExecuteCommand();
                        }
                    }
                }
                return outputMessage;
            }

            private string CheckForHandleQueuedPackageEditJob()
            {
                string outputMessage;
                using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
                {
                    using (var dbExecutor = new SqlExecutor(sqlConnection))
                    {
                        sqlConnection.Open();
                        //get the edits that are pending for more than 3 hours. Get only the edits that are submitted today ( else there are some stale pneding edits which are 4/5 months old and they will keep showing up.
                        var ErrorPendingEditCount = dbExecutor.Query<Int32>(string.Format("select count(*) from dbo.PackageEdits where TimeStamp <= '{0}' and TimeStamp >= '{1}'", DateTime.UtcNow.AddHours(thresholdValues.PendingErrorThresholdInHours * -1).ToString("yyyy-MM-dd HH:mm:ss"), DateTime.UtcNow.AddHours( -24).ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();
                        var WarningPendingEditCount = dbExecutor.Query<Int32>(string.Format("select count(*) from dbo.PackageEdits where TimeStamp <= '{0}' and TimeStamp >= '{1}'", DateTime.UtcNow.AddHours(thresholdValues.PendingWarningThresholdInHours * -1).ToString("yyyy-MM-dd HH:mm:ss"), DateTime.UtcNow.AddHours(-24).ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();
                        outputMessage = string.Format("No of pending edits is {0}. Acceptable Error lag in no. of hours: {1}", ErrorPendingEditCount,thresholdValues.PendingErrorThresholdInHours);
                        if (ErrorPendingEditCount > 0)
                        {
                            string urlLog = getLastInvocation("HandlePackageEdits", 2);
                            new SendAlertMailTask
                            {
                                AlertSubject = "Error: Work service job background check alert activated for HandleQueuedPackageEdits job",
                                Details = outputMessage + Environment.NewLine + string.Format("last two log url is {0}", urlLog),
                                AlertName = "Error: Alert for HandleQueuedPackageEdits",
                                Component = "HandleQueuedPackageEdits Job",
                                Level = "Error"
                            }.ExecuteCommand();
                        }
                        else if (WarningPendingEditCount > 0)
                        {
                            string urlLog = getLastInvocation("HandlePackageEdits", 2);
                            new SendAlertMailTask
                            {
                                AlertSubject = "Warning: Work service job background check alert activated for HandleQueuedPackageEdits job",
                                Details = string.Format("No of pending edits is {0}. Acceptable Warning lag in no. of hours: {1}, last two log url is {2}", WarningPendingEditCount, thresholdValues.PendingWarningThresholdInHours, urlLog),
                                AlertName = "Warning: Alert for HandleQueuedPackageEdits",
                                Component = "HandleQueuedPackageEdits Job",
                                Level = "Warning"
                            }.ExecuteCommand();
                        }
                    }
                }
                return outputMessage;
            }

           private string CheckForBackupPackagesJob()
           {
               string outputMessage;
               //no of new packages uploaded in the last 2 hours.
               int ErrorNewPackageCount = 0;
               int WarningNewPackageCount = 0;
               using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
               {
                   using (var dbExecutor = new SqlExecutor(sqlConnection))
                   {
                       sqlConnection.Open();
                       ErrorNewPackageCount = dbExecutor.Query<Int32>(string.Format("SELECT Count (*) FROM [dbo].[Packages] where [Created] >= '{0}'", DateTime.UtcNow.AddHours(thresholdValues.BackupPackagesErrorThresholdInHours * -1).ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();
                       WarningNewPackageCount = dbExecutor.Query<Int32>(string.Format("SELECT Count (*) FROM [dbo].[Packages] where [Created] >= '{0}'", DateTime.UtcNow.AddHours(thresholdValues.BackupPackagesWarningThresholdInHours * -1).ToString("yyyy-MM-dd HH:mm:ss"))).SingleOrDefault();
                   }
               }
                CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(PackagesContainerName);
                CloudBlobContainer destinationContainer = blobClient.GetContainerReference(BackupPackagesContainerName);
                int sourceCount = container.ListBlobs().Count();
                int destCount = destinationContainer.ListBlobs().Count(); // ListBlobs() call might take quite some time. Need to check if we can set any attributes on the container regarding package count.
                int diff = sourceCount - destCount;
                outputMessage = string.Format("No of packages yet to be backed up is {0}.",diff);
               //BackupPackages job runs every 10 minutes. But a 2 hour buffer is given just in case if there is a delay in the backup process.
               if(diff > ErrorNewPackageCount)
               {
                   string urlLog = getLastInvocation("BackupPackages", 2);
                   new SendAlertMailTask
                   {
                       AlertSubject = "Error: Work service job background check alert activated for BackupPackages job",
                       Details = outputMessage + Environment.NewLine + string.Format("last two log url is {0}", urlLog),
                       AlertName = "Error: Alert for BackupPackages",
                       Component = "BackupPackages Job",
                       Level = "Error"
                   }.ExecuteCommand();
               }
               else if (diff > WarningNewPackageCount)
               {
                   string urlLog = getLastInvocation("BackupPackages", 2);
                   new SendAlertMailTask
                   {
                       AlertSubject = "Warning: Work service job background check alert activated for BackupPackages job",
                       Details = outputMessage + Environment.NewLine + string.Format("last two log url is {0}", urlLog),
                       AlertName = "Warning: Alert for BackupPackages",
                       Component = "BackupPackages Job",
                       Level = "Warning"
                   }.ExecuteCommand();
               }
               return outputMessage;
           }

           private string getLastInvocation(string workJobName, int number)
           {
               NetworkCredential nc = new NetworkCredential(WorkServiceUserName, WorkServiceAdminKey);
               WebRequest request = WebRequest.Create(string.Format("{0}/instances/{1}?limit={2}", WorkServiceEndpoint, workJobName, number));
               request.Credentials = nc;
               request.PreAuthenticate = true;
               request.Method = "GET";
               WebResponse respose = request.GetResponse();
               StringBuilder sb = new StringBuilder();
               using (var reader = new StreamReader(respose.GetResponseStream()))
               {
                   JavaScriptSerializer js = new JavaScriptSerializer();
                   var objects = js.Deserialize<List<WorkJobInvocation>>(reader.ReadToEnd());
                   foreach (WorkJobInvocation job in objects)
                   {
                       sb.Append(job.logUrl+"\n");
                   }
               }
               return sb.ToString();
           }

        private string CheckoutForPackageStatics()
        {
            string outputMessage;
            List<DbEntry> prodDB;
            List<DbEntry> warehouseDB;
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var content = dbExecutor.Query<DbEntry>(string.Format(prodSQL, DateTime.UtcNow.AddDays(-1).ToString("MM/dd/yyyy"), DateTime.UtcNow.ToString("MM/dd/yyyy")));
                    prodDB = content.ToList<DbEntry>();
                }
            }

            using (var sqlConnection = new SqlConnection(WarehouseDb.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var content = dbExecutor.Query<DbEntry>(string.Format(wareSQL, DateTime.UtcNow.AddDays(-1).ToString("MM/dd/yyyy")));
                    warehouseDB = content.ToList<DbEntry>();
                }
            }

            bool correct = true;
            string[] Operation = new JavaScriptSerializer().Deserialize<string[]>(ReportHelpers.Load(StorageAccount, "OperationType.json", ContainerName));
            Dictionary<string, int> proddict = GenerateDict(prodDB, Operation);
            Dictionary<string, int> warehousedict = GenerateDict(warehouseDB, Operation);

            if (Math.Abs(warehousedict.Count - proddict.Count) > 10) 
            {
                bool prod = true;
                correct = false;
                StringBuilder sb = new StringBuilder();
                if (warehousedict.Count > proddict.Count) prod = false;
                if(prod)
                {
                    sb.Append("prod key is more than warehouse, the following is in prod but not in warehouse. detail: ");
                    foreach (string key in proddict.Keys)
                    {
                        if (!warehousedict.ContainsKey(key)) sb.Append(key + Environment.NewLine);
                    }
                }
                else
                {
                    sb.Append("warehouse key is more than prod, the following is in warehouse but not in prod. detail: ");
                    foreach (string key in warehousedict.Keys)
                    {
                        if (!proddict.ContainsKey(key)) sb.Append(key + Environment.NewLine);
                    }
                }
                outputMessage = string.Format("Package statistic total pacakage number is not correct on {0},more detail is {1}", DateTime.UtcNow.AddDays(-1).ToString("MM/dd/yyyy"), sb.ToString());
            }

            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (string key in proddict.Keys)
                {
                    if (!warehousedict[key].Equals(proddict[key]))
                    {
                        correct = false;
                        sb.Append(key + " ");
                    }
                }
                outputMessage = string.Format("Package statistic is not correct on {0}, following package stat is not right, which are {1}", DateTime.UtcNow.AddDays(-1).ToString("MM/dd/yyyy"),sb.ToString());
            }

            if (!correct)
            {
                new SendAlertMailTask
                {
                    AlertSubject = "Error: Work service job background check alert activated for Package Statistics job",
                    Details = outputMessage,
                    AlertName = "Error: Alert for Package Statistics",
                    Component = "Package Statistics Job",
                    Level = "Error"
                }.ExecuteCommand();
            }
            return outputMessage;

        }

        private Dictionary<string, int> GenerateDict(List<DbEntry> DB, string[] Operation)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            foreach (DbEntry each in DB)
            {
                if (!Operation.Contains(each.Operation)) each.Operation = "(unknown)";
                string key = each.PackageId + each.PackageVersion + each.Operation;
                if (dict.ContainsKey(key)) dict[key] += each.DownloadCount;
                else
                {
                    dict[key] = each.DownloadCount;
                }
            }
            return dict;
        }

        private class DbEntry
        {
            public int DownloadCount { get; set; }
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public string Operation { get; set; }

            
        }

        #endregion PrivateMethods
    }    
}

