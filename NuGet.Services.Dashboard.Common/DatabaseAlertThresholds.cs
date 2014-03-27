using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class DatabaseAlertThresholds
    {
        public int DatabaseConnectionsThreshold = 100;
        public int DatabaseRequestsThreshold = 80;
        public int DatabaseBlockedRequestsThreshold = 1;
        public int DatabaseThrottlingEventThreshold = 1;
    }
}
