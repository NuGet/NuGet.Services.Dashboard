using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("CreateWorkJobDetailReportTask", "Creates the report for the details of work job.", AltName = "cwjdrt")]
    class CreateWorkJobDetailReportTask : StorageTask
    {
        [Option("WorkServiceUserName", AltName = "name")]
        public string WorkServiceUserName { get; set; }
       
        [Option("WorkServiceAdminKey", AltName = "key")]
        public string WorkServiceAdminKey { get; set; }

        [Option("ConnectUrl", AltName = "url")]
        public string ConnectUrl { get; set; }
        
        public override void ExecuteCommand()
        {
            int lastNhour = 24;
            string env;
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            var content = ReportHelpers.Load(StorageAccount,"Configuration.WorkJobInstances.json",ContainerName);
            List<WorkJobInstanceDetails> instanceDetails = new JavaScriptSerializer().Deserialize<List<WorkJobInstanceDetails>>(content);
            if (ConnectUrl.Contains("int")) env = "Int0";
            else env = "Prod0";
            foreach (WorkJobInstanceDetails job in instanceDetails)
            {
                int invocationCount = 0;
                double totalRunTime = 0;
                int faultCount = 0;
                int faultRate = 0;
                int runtime = 0;
                Dictionary<string, List<string>> ErrorList = new Dictionary<string, List<string>>();
                NetworkCredential nc = new NetworkCredential(WorkServiceUserName, WorkServiceAdminKey);
                WebRequest request = WebRequest.Create(string.Format("{0}/instances/{1}?limit={2}",ConnectUrl,job.JobInstanceName, (lastNhour * 60) / job.FrequencyInMinutes));
                request.Credentials = nc;
                request.PreAuthenticate = true;
                request.Method = "GET";
                WebResponse respose = request.GetResponse();
                using (var reader = new StreamReader(respose.GetResponseStream()))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var objects = js.Deserialize<List<WorkJobInvocation>>(reader.ReadToEnd());
                    WorkJobInvocation lastJob;
                    string lastCompleted = string.Empty;
                    if (objects.Any((item => item.status.Equals("Executed") && item.result.Equals("Completed"))))
                    {
                        lastJob = objects.Where(item => item.status.Equals("Executed") && item.result.Equals("Completed")).ToList().FirstOrDefault();
                    }
                    else
                    {
                        lastJob = objects.FirstOrDefault();
                    }

                   if (lastJob != null)
                    {
                        lastCompleted = string.Format("{0} mins ago", Convert.ToInt32(DateTime.Now.Subtract(lastJob.completedAt).TotalMinutes));
                    }
                    else
                    {
                        lastCompleted = "N/A";
                    }

                    foreach (WorkJobInvocation each in objects)
                    {
                        if (each.completedAt >= DateTime.Now.AddHours(-lastNhour))
                        {
                            invocationCount++;
                            totalRunTime += each.completedAt.Subtract(each.queuedAt).TotalSeconds;
                            if (each.result.Equals("Faulted"))
                            {
                                faultCount++;
                                string message = getResultMessage(each.resultMessage);
                                if (ErrorList.ContainsKey(message))
                                {
                                    if (ErrorList[message].Count < 5) ErrorList[message].Add(each.logUrl);
                                }

                                else
                                {
                                    List<string> LogUrl = new List<string>();
                                    LogUrl.Add(each.logUrl);
                                    ErrorList.Add(message, LogUrl);
                                }
                            }
                        }
                    }
                    if (invocationCount != 0)
                    {
                        faultRate = (faultCount * 100 / invocationCount);
                        runtime = ((int)(totalRunTime / invocationCount));
                    }
                    jobDetail.Add(new WorkInstanceDetail(job.JobInstanceName, job.FrequencyInMinutes+ "mins",lastCompleted , runtime.ToString() + "s", invocationCount.ToString(), faultCount.ToString(), faultRate, ErrorList));
                    if (faultRate >= 30)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Alert for {0} work job service failure", env),
                            Details = string.Format("Rate of failure exceeded threshold for {0}. Threshold count : {1}, failure in last 24 hour : {2}", job.JobInstanceName, "30%", faultCount),
                            AlertName = "Work job service",
                            Component = "work job service"
                        }.ExecuteCommand();
                    }

                }
            }

             var json = new JavaScriptSerializer().Serialize(jobDetail);
             ReportHelpers.CreateBlob(StorageAccount, env+"WorkJobDetail.json", ContainerName, "application/json", ReportHelpers.ToStream(json));
        }

        private string getResultMessage(string message)
        {
            if (message.Contains("StatusMessage:"))
            {
                int start = message.IndexOf("StatusMessage:") + "StatusMessage:".Length;
                int last = start;
                while (message[last] != '\r') last++;
                return message.Substring(start, last - start);
            }
            else
            {
                int last = message.IndexOf("End of stack trace from previous location where exception was thrown");
                return message.Substring(0, last);
            }
        }
    }
}
