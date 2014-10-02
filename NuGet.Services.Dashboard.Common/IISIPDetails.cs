using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class IISIPDetails
    {
        public string cip;
        public int AvgTimeTakenInMilliSeconds;
        public int RequestsPerHour;

        public IISIPDetails()
        {

        }
        public IISIPDetails(string cip,  int avgTime, int requestsPerHour)
        {
            this.cip = cip;
            this.AvgTimeTakenInMilliSeconds = avgTime;
            this.RequestsPerHour = requestsPerHour;
        }
    }
}
