using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("CreateWorkJobWeeklyReportTask", "Creates the report for the details of work job.", AltName = "cwjwrt")]
    class CreateWorkJobWeeklyReportTask : StorageTask
    {
        [Option("WorkServiceUserName", AltName = "name")]
        public string WorkServiceUserName { get; set; }

        [Option("Work-0-ServiceAdminKey", AltName = "key0")]
        public string WorkServiceAdminKey { get; set; }

        [Option("Work-1-ServiceAdminKey", AltName = "key1")]
        public string WorkServiceFailoverAdminKey { get; set; }

        [Option("SubsciptionId", AltName = "lid")]
        public string SubscriptionId { get; set; }

        [Option("CloudServiceId", AltName = "cid")]
        public string CloudServiceId { get; set; }

        [Option("JobId", AltName = "jid")]
        public string JobId { get; set; }

        [Option("CertificateName", AltName = "cername")]
        public string CertificateName { get; set; }

        public override void ExecuteCommand()
        {
            int lastNhour = 24;
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            List<WorkJobInstanceDetails> instanceDetails = getWorkjobInstance();

            foreach (WorkJobInstanceDetails job in instanceDetails)
            {
                int invocationCount = 0;
                double totalRunTime = 0;
                int faultCount = 0;
                int faultRate = 0;
                int runtime = 0;
                Dictionary<string, List<string>> ErrorList = new Dictionary<string, List<string>>();
                string AdminKey = WorkServiceAdminKey;

                if (job.url.Contains("api-work-1"))
                {
                    AdminKey = WorkServiceFailoverAdminKey;
                }
                NetworkCredential nc = new NetworkCredential(WorkServiceUserName, AdminKey);
                WebRequest request = WebRequest.Create(string.Format("{0}/instances/{1}?limit={2}", job.url, job.JobInstanceName, (lastNhour * 60) / job.FrequencyInMinutes));
                request.Credentials = nc;
                request.PreAuthenticate = true;
                request.Method = "GET";
                WebResponse respose = request.GetResponse();
                using (var reader = new StreamReader(respose.GetResponseStream()))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    js.MaxJsonLength = Int32.MaxValue;
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
                    jobDetail.Add(new WorkInstanceDetail(job.JobInstanceName, job.FrequencyInMinutes + "mins", lastCompleted, runtime.ToString() + "s", invocationCount.ToString(), faultCount.ToString(), faultRate, ErrorList));
                }
            }

            List<WorkServiceAdmin> allkey = new List<WorkServiceAdmin>();
            allkey.Add(new WorkServiceAdmin(WorkServiceUserName, WorkServiceAdminKey));
            allkey.Add(new WorkServiceAdmin(WorkServiceUserName, WorkServiceFailoverAdminKey));

            var json = new JavaScriptSerializer().Serialize(jobDetail);
            var key = new JavaScriptSerializer().Serialize(allkey);
            ReportHelpers.CreateBlob(StorageAccount, string.Format("WorkJobDetail{0:MMdd}.json",DateTime.Now), ContainerName, "application/json", ReportHelpers.ToStream(json));
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

        private List<WorkJobInstanceDetails> getWorkjobInstance()
        {
            X509Certificate cert = X509Certificate.CreateFromCertFile(CertificateName);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://management.core.windows.net/{0}/cloudservices/{1}/resources/scheduler/~/JobCollections/{2}/jobs?api-version=2014-04-01 ", SubscriptionId, CloudServiceId, JobId));
            request.ClientCertificates.Add(cert);
            request.Headers.Add("x-ms-version: 2013-03-01");
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();

            List<WorkJobInstanceDetails> instanceDetails = new List<WorkJobInstanceDetails>();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var summaryObject = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var summary in summaryObject)
                {

                    int FrequencyInMinutes = 0;
                    if (summary["recurrence"]["frequency"].Equals("minute")) FrequencyInMinutes = summary["recurrence"]["interval"];
                    if (summary["recurrence"]["frequency"].Equals("hour")) FrequencyInMinutes = summary["recurrence"]["interval"] * 60;
                    if (summary["recurrence"]["frequency"].Equals("day")) FrequencyInMinutes = summary["recurrence"]["interval"] * 60 * 24;
                    instanceDetails.Add(new WorkJobInstanceDetails(summary["id"], FrequencyInMinutes, summary["action"]["request"]["uri"]));
                }
                return instanceDetails;
            }
        }
    }
}