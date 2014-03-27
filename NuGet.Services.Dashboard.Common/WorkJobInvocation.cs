using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class WorkJobInvocation
    {
        public Guid id; 
        public string job; 
        public string jobInstanceName; 
        public string source;    
        public string status;
        public string result;
        public string lastUpdatedBy;
        public string  logUrl;
        public int dequeueCount;
        public bool isContinuation;
        public DateTime lastDequeuedAt;
        public DateTime completedAt; 
        public DateTime queuedAt; 
        public DateTime nextVisibleAt;
        public DateTime updatedAt;
    }
}
