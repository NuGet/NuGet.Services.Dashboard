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

        [Option("WorkServiceEndpoint", AltName = "url")]
        public string WorkServiceEndpoint { get; set; }

        [Option("FailoverEndpoint", AltName = "furl")]
        public string FailoverEndpoint { get; set; }
        
        public override void ExecuteCommand()
        {
            int lastNhour = 24;
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            var content = ReportHelpers.Load(StorageAccount,"Configuration.WorkJobInstances.json",ContainerName);
            List<WorkJobInstanceDetails> instanceDetails = new JavaScriptSerializer().Deserialize<List<WorkJobInstanceDetails>>(content);
            foreach (WorkJobInstanceDetails job in instanceDetails)
            {
                int invocationCount = 0;
                double totalRunTime = 0;
                int faultCount = 0;
                int faultRate = 0;
                int runtime = 0;
                string Endpoint = WorkServiceEndpoint;
                if (job.JobInstanceName.Contains("FailoverDC")) Endpoint = FailoverEndpoint;
                Dictionary<string, List<string>> ErrorList = new Dictionary<string, List<string>>();
                NetworkCredential nc = new NetworkCredential(WorkServiceUserName, WorkServiceAdminKey);
                WebRequest request = WebRequest.Create(string.Format("{0}/instances/{1}?limit={2}", Endpoint, job.JobInstanceName, (lastNhour * 60) / job.FrequencyInMinutes));
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
                    AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
                    if (faultRate > thresholdValues.WorkJobErrorThreshold)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Error: Alert for work job service : {0} failure", job.JobInstanceName),
                            Details = string.Format("Rate of failure exceeded Error threshold for {0}. Threshold count : {1}%, failure in last 24 hour : {2}", job.JobInstanceName,thresholdValues.WorkJobErrorThreshold , faultCount),
                            AlertName = "Error: Work job service",
                            Component = "work job service",
                            Level = "Error"
                        }.ExecuteCommand();
                    }
                    else if (faultRate > thresholdValues.WorkJobWarningThreshold)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Warning: Alert for work job service: {0} failure", job.JobInstanceName),
                            Details = string.Format("Rate of failure exceeded Warning threshold for {0}. Threshold count : {1}%, failure in last 24 hour : {2}", job.JobInstanceName, thresholdValues.WorkJobWarningThreshold, faultCount),
                            AlertName = "Warning: Work job service",
                            Component = "work job service",
                            Level = "Warning"
                        }.ExecuteCommand();
                    }

                }
            }

             var json = new JavaScriptSerializer().Serialize(jobDetail);
             ReportHelpers.CreateBlob(StorageAccount, "WorkJobDetail.json", ContainerName, "application/json", ReportHelpers.ToStream(json));
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
                if (last < 0) return message;
                return message.Substring(0, last);
            }
        }
    }
}
