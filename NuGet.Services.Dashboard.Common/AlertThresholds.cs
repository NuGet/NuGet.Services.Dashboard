﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class AlertThresholds
    {
        public int DatabaseConnectionsThreshold = 150; // Specifies the limit on maximum number of DB connections on Gallery DB.
        public int DatabaseRequestsThreshold = 80; // Specifies the limit on the maximum number of DB requests on Gallery DB. 
        public int DatabaseBlockedRequestsThreshold = 20; // Specifies the limit on the number of suspended/Blocked requests in DB.
        public int DatabaseThrottlingEventThreshold = 10;
        public int DatabaseIndexFragmentationPercentThreshold = 20;
        public int DatabaseSizeWarningPercentThreshold = 75; //specifies the warning limit on DB size (% used in maxsize)
        public int DatabaseSizeErrorPercentThreshold = 90;  //specifies the error limit on DB size (% used in maxsize)
        public int ElmahCriticalErrorPerHourAlertThreshold = 200;// specifies the limit on the number of critical errors in a specific category per hour in Elmah.
        public int LuceneIndexLagAlertThreshold = 100; // Specifies the limit on the delta between number of packages in DB and Lucene Index.
        public int BackupDBAgeThresholdInMinutes = 120; //A back up no older than 1 hour should be present. But make it 2 hours just to provide extra buffer if in case the worker job is taking time.
        public int OnlineDBBackupsThreshold = 7;// Only 4 backup can be present online at any point of time. But make the threshold 7 to provide some space for slow running jobs.
        public int PurgeStatisticsThresholdInDays = 8; //Purge stats job purges records older than 7 days. Settings the threshold to 8 to provide some buffer.
        public int PendingThresholdInHours = 3; //Specifies the limit on the no. of hours a package can remain in Pending state.
        public int BackupPackagesThresholdInHours = 2; //Specifies the limit lag between "packages" container and "backup" container.


    }
}