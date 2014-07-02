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
        public string resultMessage;
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
    public class WorkInstanceDetail
    {
        public string jobName;
        public string Frequency;   
        public string LastTime;
        public string RunTime;
        public string InvocationNo;
        public string FaultedNo;
        public int FaultRate;
        public Dictionary<string,List<string>> ErrorMessage;

        public WorkInstanceDetail(string jobName, string Frequency, string LastTime, string RunTime, string InvocationNo, string FaultedNo, int FaultRate,Dictionary<string, List<string>> ErrorMessage)
        {
            this.jobName = jobName;
            this.Frequency = Frequency;
            this.LastTime = LastTime;
            this.RunTime = RunTime;
            this.InvocationNo = InvocationNo;
            this.FaultedNo = FaultedNo;
            this.FaultRate = FaultRate;
            this.ErrorMessage = ErrorMessage;
        }

        public WorkInstanceDetail()
        {

        }
    }

    public class WorkServiceAdmin
    {
        public string username;
        public string key;

        public WorkServiceAdmin(string username, string key)
        {
            this.username = username;
            this.key = key;
        }

        public WorkServiceAdmin()
        {

        }
    }
}
