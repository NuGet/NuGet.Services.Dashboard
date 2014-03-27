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

        public WorkJobInstanceDetails(string jobInstanceName,int repeatFrequency)
        {           
            this.JobInstanceName = jobInstanceName;
            this.FrequencyInMinutes = repeatFrequency;
        }
        public WorkJobInstanceDetails()
        {           
           
        }
    }
}
