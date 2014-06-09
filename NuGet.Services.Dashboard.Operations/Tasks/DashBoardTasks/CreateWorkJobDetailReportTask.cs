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
            //List<Tuple<string, string, string, string>> jobResults = GetJobDetail();
            var content = ReportHelpers.Load(StorageAccount,"Configuration.WorkJobInstances_test.json",ContainerName);
            List<WorkJobInstanceDetails> instanceDetails = new JavaScriptSerializer().Deserialize<List<WorkJobInstanceDetails>>(content);
            if (ConnectUrl.Contains("int")) env = "Int0";
            else env = "Prod0";
            foreach (WorkJobInstanceDetails job in instanceDetails)
            {
                int invocationCount = 0;
                double totalRunTime = 0;
                int faultCount = 0;
                int faultRate = 0;
                Dictionary<string, List<string>> ErrorList = new Dictionary<string, List<string>>();
                //WorkJobInstanceDetails tmp = instanceDetails.Find(x => x.JobInstanceName.Equals(job.Item1));
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
                    bool jobStatus;
                    string lastCompleted = string.Empty;
                    if (objects.Any((item => item.status.Equals("Executed") && item.result.Equals("Completed"))))
                    {
                        lastJob = objects.Where(item => item.status.Equals("Executed") && item.result.Equals("Completed")).ToList().FirstOrDefault();
                        if (DateTime.Now.Subtract(lastJob.completedAt) > new TimeSpan(0, 2 * job.FrequencyInMinutes, 0)) //the time interval from the latest successful job instance cannot be more than twice the frequency.
                        {
                            jobStatus = false;
                        }
                        else
                        {
                            jobStatus = true;
                        }
                    }
                    else
                    {
                        lastJob = objects.FirstOrDefault();
                        jobStatus = false;
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
                    faultRate = (faultCount * 100 / invocationCount);
                    jobDetail.Add(new WorkInstanceDetail(job.JobInstanceName, job.FrequencyInMinutes+ "mins", jobStatus.ToString(),lastCompleted , ((int)(totalRunTime / invocationCount)).ToString() + "s", invocationCount.ToString(), faultCount.ToString(), faultRate, ErrorList));
                    if (faultRate >= 30)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Alert for {0} work job service failure",env),
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
            else return message;
        }
    }
}
