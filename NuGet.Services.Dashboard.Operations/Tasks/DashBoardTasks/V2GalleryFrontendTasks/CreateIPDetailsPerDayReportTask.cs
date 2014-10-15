using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using AnglicanGeek.DbExecutor;
using System;
using System.Net;
using System.Web.Script.Serialization;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using Elmah;
using System.Diagnostics;
using NuGet.Services.Dashboard.Common;

namespace NuGetGallery.Operations
{
    [Command("CreateIPDetailsPerDayReportTask", "Creates the IP details per day report from IIS logs", AltName = "criprt")]
    public class CreateIPDetailsPerDayReportTask : StorageTask
    {
        [Option("IISStorageAccount", AltName = "iis")]
        public CloudStorageAccount IISStorageAccount { get; set; }

        [Option("Retry", AltName = "retry")]
        public int RetryCount { get; set; }

        [Option("ServiceName", AltName = "servicename")]
        public string ServiceName { get; set; }

        public string ReportDate
        {
            get
            {
                return string.Format("{0:yyMMdd}", DateTime.UtcNow.AddDays(-1));
            }
        }

        public override void ExecuteCommand()
        {
            //Get the logs for the previous day.
            string DeploymentID = new JavaScriptSerializer().Deserialize<string>(ReportHelpers.Load(StorageAccount, "DeploymentId_" + ServiceName + ".json", ContainerName));
            string blobName = DeploymentID + "/NuGetGallery/NuGetGallery_IN_{IID}/Web/W3SVC1273337584/u_ex{Date}{Hour}.log";
            blobName = blobName.Replace("{Date}", ReportDate);

            DirectoryInfo info = new System.IO.DirectoryInfo(Environment.CurrentDirectory);

            //Downlog the logs for the last day.

            while (RetryCount-- > 0)
            {
                try
                {
                    if (!Directory.Exists(info.FullName))
                    {
                        Directory.CreateDirectory(info.FullName);
                    }

                    int instanceCount = GetCurrentInstanceCountInGallery(); //get current instance count.
                    for (int i = 0; i < instanceCount; i++)
                    {
                        for (int j = 0; j < 24; j++) //Download the log for each hour
                        {
                            string temp = blobName;
                            temp = temp.Replace("{IID}", i.ToString());
                            temp = temp.Replace("{Hour}", j.ToString("00"));

                            string logName = temp.Substring(temp.LastIndexOf("/") + 1);
                            logName = logName.Substring(0, logName.Length - 4);
                            string localFile = Path.Combine(info.FullName, logName + i.ToString() + "_" + j.ToString() + ".log");
                            ReportHelpers.DownloadBlobToLocalFile(IISStorageAccount, temp, localFile, "wad-iis-requestlogs");
                        }
                    }
                    break; // break if the operation succeeds without doing any retry.
                }
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("Exception thrown while trying to create report : {0}", e.Message));
                }
            }

            //Create a json file
            CreateIPDetailsReport(info);
            CreateResponseTimeReport(info);
            CreateUserAgentReport(info);
        }

        private void CreateIPDetailsReport(DirectoryInfo info)
        {
            string standardError = string.Empty;
            string standardOutput = string.Empty;
            List<IISIPDetails> ipDetails = new List<IISIPDetails>();

            string query = string.Format(@"select c-ip, avg(time-taken), count(*) from {0}\*{1}*.log GROUP BY c-ip", info.FullName, ReportDate);
            ipDetails = InvokeLogParserProcessForIPDetails(@"-i:IISW3C -o:CSV " + @"""" + query + @"""" + " -stats:OFF", 3);
            if (ipDetails.Count > 0)
            {
                string blobName = "IISIPDetails" + ReportDate + ".json";
                int count = 0;
                foreach (IISIPDetails detail in ipDetails)
                {
                    var json = new JavaScriptSerializer().Serialize(detail);
                    ReportHelpers.AppendDatatoBlob(StorageAccount, blobName, new Tuple<string, string>(count.ToString(), json), ipDetails.Count, ContainerName);
                    count++;
                }
            }

        }


        private void CreateResponseTimeReport(DirectoryInfo info)
        {
            string standardError = string.Empty;
            string standardOutput = string.Empty;
            List<IISResponseTimeDetails> responseTimeDetails = new List<IISResponseTimeDetails>();

            string query = string.Format(@"select cs-uri-stem, avg(time-taken) from {0}\*{1}*.log GROUP BY cs-uri-stem", info.FullName, ReportDate);
            responseTimeDetails = InvokeLogParserProcessForResponseTime(@"-i:IISW3C -o:CSV " + @"""" + query + @"""" + " -stats:OFF", 2);
            if (responseTimeDetails.Count > 0)
            {
                string blobName = "IISResponseTimeDetails" + ReportDate + ".json";
                int count = 0;
                foreach (IISResponseTimeDetails detail in responseTimeDetails)
                {
                    var json = new JavaScriptSerializer().Serialize(detail);
                    ReportHelpers.AppendDatatoBlob(StorageAccount, blobName, new Tuple<string, string>(count.ToString(), json), responseTimeDetails.Count, ContainerName);
                    count++;
                }
            }

        }

        private void CreateUserAgentReport(DirectoryInfo info)
        {
            string standardError = string.Empty;
            string standardOutput = string.Empty;
            List<IISUserAgentDetails> userAgentDetails = new List<IISUserAgentDetails>();
            var content = ReportHelpers.Load(StorageAccount, "Configuration.IISUserAgent.json", ContainerName);
            List<IISUserAgentDetails> userAgents = new List<IISUserAgentDetails>();
            userAgents = new JavaScriptSerializer().Deserialize<List<IISUserAgentDetails>>(content);
            foreach (IISUserAgentDetails agent in userAgents)
            {
                string query = string.Format(@"select count(*) from {0}\*{1}*.log WHERE cs(User-Agent) LIKE '{2}'", info.FullName, ReportDate, agent.UserAgent);
                int requestCount = InvokeLogParserProcessForUserAgent(@"-i:IISW3C -o:CSV " + @"""" + query + @"""" + " -stats:OFF");
                int avgTime = 0;
                if (requestCount > 0)
                {
                    query = string.Format(@"select avg(time-taken) from {0}\*{1}*.log WHERE cs(User-Agent) LIKE '{2}'", info.FullName, ReportDate, agent.UserAgent);
                    avgTime = InvokeLogParserProcessForUserAgent(@"-i:IISW3C -o:CSV " + @"""" + query + @"""" + " -stats:OFF");
                }
                userAgentDetails.Add(new IISUserAgentDetails(agent.UserAgentName, agent.UserAgent, avgTime, requestCount));
            }
           
            string blobName = "IISUserAgentDetails" + ReportDate + ".json";
            int count = 0;
                foreach (IISUserAgentDetails detail in userAgentDetails)
                {
                    var json = new JavaScriptSerializer().Serialize(detail);
                    ReportHelpers.AppendDatatoBlob(StorageAccount, blobName, new Tuple<string, string>(count.ToString(), json), userAgentDetails.Count, ContainerName);
                    count++;
                }

        }


        private List<IISIPDetails> InvokeLogParserProcessForIPDetails(string arguments, int numFields)
        {
            List<IISIPDetails> ipDetails = new List<IISIPDetails>();

            Process nugetProcess = new Process();
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "LogParser.exe"));
            nugetProcessStartInfo.Arguments = arguments;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
            nugetProcess.StartInfo = nugetProcessStartInfo;
            nugetProcess.Start();

            StreamReader reader = nugetProcess.StandardOutput;
            reader.ReadLine(); //this has the field names, ignore it.
            while (!reader.EndOfStream)
            {
                string ipOutput = reader.ReadLine();

                string[] ipOutputs = ipOutput.Split(',');
                if (ipOutputs.Count() == numFields)
                {
                    ipDetails.Add(new IISIPDetails(ipOutputs[0], Convert.ToInt32(ipOutputs[1]), Convert.ToInt32(ipOutputs[2])));
                }
            }

            nugetProcess.WaitForExit();
            List<IISIPDetails> filteredList = new List<IISIPDetails>();
            filteredList = ipDetails.OrderByDescending(x => x.RequestsPerHour).Take(15).ToList();
            return filteredList;
        }

        private List<IISResponseTimeDetails> InvokeLogParserProcessForResponseTime(string arguments, int numFields)
        {
            List<IISResponseTimeDetails> responseTimeDetails = new List<IISResponseTimeDetails>();

            Process nugetProcess = new Process();
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "LogParser.exe"));
            nugetProcessStartInfo.Arguments = arguments;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
            nugetProcess.StartInfo = nugetProcessStartInfo;
            nugetProcess.Start();

            StreamReader reader = nugetProcess.StandardOutput;
            reader.ReadLine(); //this has the field names, ignore it.
            while (!reader.EndOfStream)
            {
                string responseTimeOutput = reader.ReadLine();

                string[] responseTimeOutputs = responseTimeOutput.Split(',');
                if (responseTimeOutputs.Count() == numFields)
                {
                    responseTimeDetails.Add(new IISResponseTimeDetails(responseTimeOutputs[0], Convert.ToInt32(responseTimeOutputs[1])));
                }
            }

            nugetProcess.WaitForExit();
            List<IISResponseTimeDetails> filteredList = new List<IISResponseTimeDetails>();
            filteredList = responseTimeDetails.OrderByDescending(x => x.AvgTimeTakenInMilliSeconds).Take(15).ToList();
            return filteredList;
        }

        private int InvokeLogParserProcessForUserAgent(string arguments)
        {
            Process nugetProcess = new Process();
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "LogParser.exe"));
            nugetProcessStartInfo.Arguments = arguments;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
            nugetProcess.StartInfo = nugetProcessStartInfo;
            nugetProcess.Start();
            nugetProcess.StandardOutput.ReadLine(); //this has the field name, ignore it
            string output = nugetProcess.StandardOutput.ReadLine();

            string metricValue = "0";
            if (!string.IsNullOrEmpty(output))
            {
                metricValue = output.Trim();
            }
            return Convert.ToInt32(metricValue);
        }

        /// <summary>
        /// Returns the instance count in Gallery cloud service for the last hour.
        /// </summary>
        /// <returns></returns>
        private int GetCurrentInstanceCountInGallery()
        {
            try
            {

                Dictionary<string, string> instanceCountDict = ReportHelpers.GetDictFromBlob(StorageAccount, ServiceName + "InstanceCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", ContainerName);
                if (instanceCountDict != null && instanceCountDict.Count > 0)
                {
                    return Convert.ToInt32(instanceCountDict.Values.ElementAt(instanceCountDict.Count - 1));
                }
                else
                {
                    return 3; //default instance count in Gallery
                }
            }
            catch
            {
                return 3; //return 3 by default as we don't want to fail if the expected blob is not present.
            }
        }
    }
}

