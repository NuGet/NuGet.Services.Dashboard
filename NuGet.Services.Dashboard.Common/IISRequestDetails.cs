using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class IISRequestDetails
    {
        public string ScenarioName;
        public string UriStem;
        public int AvgTimeTakenInMilliSeconds;
        public int RequestsPerHour;

        public IISRequestDetails()
        {

        }
        public IISRequestDetails(string scenarioName,string uriStem, int avgTime, int requestsPerHour)
        {
            this.ScenarioName = scenarioName;
            this.UriStem = uriStem;
            this.AvgTimeTakenInMilliSeconds = avgTime;
            this.RequestsPerHour = requestsPerHour;
        }
    }
}
