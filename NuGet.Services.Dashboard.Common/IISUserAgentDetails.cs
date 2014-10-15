using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class IISUserAgentDetails
    {
        public string UserAgentName;
        public string UserAgent;
        public int AvgTimeTakenInMilliSeconds;
        public int RequestsPerHour;

        public IISUserAgentDetails()
        {

        }
        public IISUserAgentDetails(string userAgentName, string userAgent, int avgTime, int requestsPerHour)
        {
            this.UserAgentName = userAgentName;
            this.UserAgent = userAgent;
            this.AvgTimeTakenInMilliSeconds = avgTime;
            this.RequestsPerHour = requestsPerHour;
        }
    }
}
