using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class WorkJobInstanceDetails
    {
    
        public string JobInstanceName;
        public int FrequencyInMinutes;
        public string url;

        public WorkJobInstanceDetails(string jobInstanceName,int repeatFrequency,string url)
        {           
            this.JobInstanceName = jobInstanceName;
            this.FrequencyInMinutes = repeatFrequency;
            this.url = url;
        }
        public WorkJobInstanceDetails()
        {           
           
        }
    }
}
