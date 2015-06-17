using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class AlertThresholds
    {
        public int DatabaseConnectionsErrorThreshold = 150; // Specifies the limit on maximum number of DB connections on Gallery DB.
        public int DatabaseRequestsErrorThreshold = 80; // Specifies the limit on the maximum number of DB requests on Gallery DB. 
        public int DatabaseBlockedRequestsErrorThreshold = 20; // Specifies the limit on the number of suspended/Blocked requests in DB.
        public int DatabaseThrottlingEventErrorThreshold = 10;
        public int DatabaseIndexFragmentationPercentErrorThreshold = 20;
        public int DatabaseSizePercentErrorThreshold = 90;  //specifies the error limit on DB size (% used in maxsize)
        public int ElmahCriticalErrorPerHourAlertErrorThreshold = 200;// specifies the limit on the number of critical errors in a specific category per hour in Elmah.
        public int LuceneIndexLagAlertErrorThreshold = 100; // Specifies the limit on the delta between number of packages in DB and Lucene Index.
        public int BackupDBAgeErrorThresholdInMinutes = 120; //A back up no older than 1 hour should be present. But make it 2 hours just to provide extra buffer if in case the worker job is taking time.
        public int OnlineDBBackupsErrorThreshold = 7;// Only 4 backup can be present online at any point of time. But make the threshold 7 to provide some space for slow running jobs.
        public int PurgeStatisticsErrorThresholdInDays = 8; //Purge stats job purges records older than 7 days. Settings the threshold to 8 to provide some buffer.
        public int PendingErrorThresholdInHours = 3; //Specifies the limit on the no. of hours a package can remain in Pending state.
        public int BackupPackagesErrorThresholdInHours = 2; //Specifies the limit lag between "packages" container and "backup" container.
        public int FailoverDBAgeErrorThresholdInMinutes = 180; //Specifies the lag between the primary DC's database and failover database in minutes.
        public int FailoverDBAndBlobLagErrorThreshold = 20; //Specifies the allowed lag between failover DB and blob in terms of number of packages.
        public int SearchCpuPercentErrorThreshold = 80; //specifies the limit on search service cpu usage (% used in maxsize)
        public int SearchMemErrorThresholdInGb = 1; // specifies the limit on search service memory usage (GB)
        public int WorkJobErrorThreshold = 30;
        public int DatabaseImportErrorThreshold = 39000;
        public int PingdomServiceDistruptionErrorThresholdInSeconds = 30; // specifies the limit on down time for each pindom micro service (second)
        public int MetricsServiceHeartbeatErrorThreshold = 30;
        public int MetricsServiceStatusErrorThreshold = 900;
        public int UploadPackageThreshold = 0; //specifies limit on latency in milliseconds for uploading a package
        public int DownloadPackageThreshold = 0; //specifies limit on latency in milliseconds for downloading a package
        public int SearchPackageThreshold = 0; //specifies limit on latency in milliseconds for searching a package
        public int CatalogLagThreshold = 0; //specifies limit on number of packages in DB, not in Catalog
        public int ResolverLagThreshold = 0; //specifies limit on time in minutes Resolver Blobs can lag behind Catalog
        public int V3LuceneIndexLagThreshold = 20; // Specifies the limit on the delta between number of packages in V2 DB and V3 Lucene Index.
        public int V3SearchIndexCommitTimeStampLagInMinutes = 30; //Specifices the allowed minutes by which the V3 lucene index can lag from datetime.now.
        public int V3CatalogCommitTimeStampLagInMinutes = 15; //specified the allowed minutes by which the V3 Catalog can lag behind V2 DB.
    

        // warning threhold
        public int DatabaseConnectionsWarningThreshold = int.MaxValue; // Specifies the limit on maximum number of DB connections on Gallery DB.
        public int DatabaseRequestsWarningThreshold = int.MaxValue; // Specifies the limit on the maximum number of DB requests on Gallery DB. 
        public int DatabaseBlockedRequestsWarningThreshold = int.MaxValue; // Specifies the limit on the number of suspended/Blocked requests in DB.
        public int DatabaseThrottlingEventWarningThreshold = int.MaxValue;
        public int DatabaseIndexFragmentationPercentWarningThreshold = int.MaxValue;
        public int DatabaseSizePercentWarningThreshold = int.MaxValue; //specifies the warning limit on DB size (% used in maxsize)
        public int ElmahCriticalErrorPerHourAlertWarningThreshold = int.MaxValue;// specifies the limit on the number of critical errors in a specific category per hour in Elmah.
        public int LuceneIndexLagAlertWarningThreshold = int.MaxValue; // Specifies the limit on the delta between number of packages in DB and Lucene Index.
        public int BackupDBAgeWarningThresholdInMinutes = 90; //A back up no older than 1 hour should be present. But make it 2 hours just to provide extra buffer if in case the worker job is taking time.
        public int OnlineDBBackupsWarningThreshold = int.MaxValue;// Only 4 backup can be present online at any point of time. But make the threshold 7 to provide some space for slow running jobs.
        public int PurgeStatisticsWarningThresholdInDays = 10; //Purge stats job purges records older than 7 days. Settings the threshold to 8 to provide some buffer.
        public int PendingWarningThresholdInHours = 4; //Specifies the limit on the no. of hours a package can remain in Pending state.
        public int BackupPackagesWarningThresholdInHours = int.MaxValue; //Specifies the limit lag between "packages" container and "backup" container.
        public int FailoverDBAgeWarningThresholdInMinutes = int.MaxValue; //Specifies the lag between the primary DC's database and failover database in minutes.
        public int FailoverDBAndBlobLagWarningThreshold = int.MaxValue; //Specifies the allowed lag between failover DB and blob in terms of number of packages.
        public int SearchCpuPercentWarningThreshold = int.MaxValue; //specifies the limit on search service cpu usage (% used in maxsize)
        public int SearchMemWarningThresholdInGb = int.MaxValue; // specifies the limit on search service memory usage (GB)
        public int WorkJobWarningThreshold = 20;
        public int DatabaseImportWarningThreshold = 0;

    }
}
