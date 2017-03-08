using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("CreateRequestsPerHourReportTask", "Creates the report for the number of requests per hour from IIS logs", AltName = "crphrt")]
    public class CreateRequestsPerHourReportTask : StorageTask
    {
        [Option("IISStorageAccount", AltName = "iis")]
        public CloudStorageAccount IISStorageAccount { get; set; }

        [Option("Retry", AltName = "retry")]
        public int RetryCount { get; set; }

        [Option("ServiceName", AltName = "servicename")]
        public string ServiceName { get; set; }



        public override void ExecuteCommand()
        {            
           //Get the logs for the previous Hour as the current one is being used by Azure.
           string DeploymentID = new JavaScriptSerializer().Deserialize<string>(ReportHelpers.Load(StorageAccount, "DeploymentId_" + ServiceName + ".json", ContainerName)); 
           string latestLogName = "u_ex" + string.Format("{0:yyMMddHH}", DateTime.UtcNow.AddHours(-1)) + ".log";
           DirectoryInfo info = new System.IO.DirectoryInfo(Path.Combine(Environment.CurrentDirectory, latestLogName));
           //Downlog the logs for the last hour.
            while (RetryCount-- > 0)
            {
                try
                {                   
                    if (!Directory.Exists(info.FullName))
                    {
                        Directory.CreateDirectory(info.FullName);
                    }                    
                 
                    int instanceCount = GetCurrentInstanceCountInGallery(); //get current instance count.
                    for (int i = 0; i < instanceCount;i++ )  
                        ReportHelpers.DownloadBlobToLocalFile(IISStorageAccount, DeploymentID + "/NuGetGallery/NuGetGallery_IN_" + i.ToString() + "/Web/W3SVC1273337584/" + latestLogName, Path.Combine(info.FullName, "IN" + i.ToString()+".log"), "wad-iis-requestlogs");
                    break; // break if the operation succeeds without doing any retry.
                }
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("Exception thrown while trying to create report : {0}", e.Message));
                }
            }
            //Create reports.
            CreateOverviewReport(info);
            CreateUriStemDetailedReport(info);         
          }

        /// <summary>
        /// Creates report for Over all RequestsPerHour
        /// </summary>
        /// <param name="info"></param>
        private  void CreateOverviewReport(DirectoryInfo info)
        {
           int requestCount = GetDataForUriStem("%", "count (*)", info.FullName);
           string blobName = "IISRequests" + string.Format("{0:MMdd}", DateTime.Now.AddHours(-1)) + ".json";
           Tuple<string, string> datapoint = new Tuple<string, string>(string.Format("{0:HH:00}", DateTime.Now.AddHours(-1)), requestCount.ToString());
           ReportHelpers.AppendDatatoBlob(StorageAccount, blobName, datapoint, 50, ContainerName);
        }

        /// <summary>
        /// Creates report for count and avg time taken for individual scenarios.
        /// </summary>
        /// <param name="info"></param>
        private  void CreateUriStemDetailedReport(DirectoryInfo info)
        {
          List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
          var content = ReportHelpers.Load(StorageAccount,"Configration.IISRequestStems.json",ContainerName);
          List<IISRequestDetails> UriStems = new List<IISRequestDetails>();
          UriStems = new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(content);
            foreach (IISRequestDetails stem in UriStems)
            {
                int requestCount = GetDataForUriStem(stem.UriStem, "count (*)", info.FullName);
                int avgTime = 0 ;
                if (requestCount > 0)
                {
                    avgTime = GetDataForUriStem(stem.UriStem, "avg (time-taken)", info.FullName);
                }
                requestDetails.Add(new IISRequestDetails(stem.ScenarioName, stem.UriStem, avgTime, requestCount));
            }
            var json = new JavaScriptSerializer().Serialize(requestDetails);
            string blobName = "IISRequestDetails" + string.Format("{0:MMdd}", DateTime.Now.AddHours(-1)) + ".json";
            ReportHelpers.AppendDatatoBlob(StorageAccount, blobName, new Tuple<string, string>(string.Format("{0:HH:00}", DateTime.Now.AddHours(-1)), json), 50, ContainerName);
        }

        
        /// <summary>
        /// Gets the data for the specific query using Logparser
        /// </summary>
        /// <param name="stem"></param>
        /// <param name="selection"></param>
        /// <param name="dirFullPath"></param>
        /// <returns></returns>
        private  int GetDataForUriStem(string stem,string selection,string dirFullPath)
        {
            string standardError = string.Empty;
            string standardOutput = string.Empty;
            string query = string.Format(@"select {0} from {1}\*.log where cs-uri-stem LIKE '{2}'", selection, dirFullPath, stem);
            int exitCode = InvokeNugetProcess(@"-i:IISW3C -o:CSV " + @"""" +query + @"""" + " -stats:OFF", out standardError, out standardOutput);
            Console.WriteLine(exitCode);
            Console.WriteLine(standardOutput);
            string metricValue = "0";
            if (!string.IsNullOrEmpty(standardOutput))
            {
                metricValue = standardOutput.Trim();
            }
            return Convert.ToInt32(metricValue);           
        }
          

        private  int InvokeNugetProcess(string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
        {
            Process nugetProcess = new Process();
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "LogParser.exe"));
            nugetProcessStartInfo.Arguments = arguments;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
            nugetProcess.StartInfo = nugetProcessStartInfo;
            nugetProcess.StartInfo.WorkingDirectory = WorkingDir;
            nugetProcess.Start(); standardError = nugetProcess.StandardError.ReadToEnd();
            nugetProcess.StandardOutput.ReadLine();
            standardOutput = nugetProcess.StandardOutput.ReadLine(); //just read the second line from the output as that is the one that has the required value.          
            nugetProcess.WaitForExit();
            return nugetProcess.ExitCode;
        }

        /// <summary>
        /// Returns the instance count in Gallery cloud service for the last hour.
        /// </summary>
        /// <returns></returns>
        private  int GetCurrentInstanceCountInGallery()
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
            }catch
            {
                return 3; //return 3 by default as we don't want to fail if the expected blob is not present.
            }
        }
    }
}

