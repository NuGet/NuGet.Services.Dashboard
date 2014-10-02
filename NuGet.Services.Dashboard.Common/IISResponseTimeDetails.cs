using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class IISResponseTimeDetails
    {
        public string UriStem;
        public int AvgTimeTakenInMilliSeconds;
        
        public IISResponseTimeDetails()
        {

        }
        public IISResponseTimeDetails(string uriStem, int avgTime)
        {
            this.UriStem = uriStem;
            this.AvgTimeTakenInMilliSeconds = avgTime;
        }
    }
}
