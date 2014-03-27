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
using System.IO;
using System.Diagnostics;

namespace NuGetGallery.Operations
{
    [Command("CreateRequestsPerHourReportTask", "Creates the report for the number of requests per hour from IIS logs", AltName = "crphrt")]
    public class CreateRequestsPerHourReportTask : StorageTask
    {
        [Option("DeploymentID", AltName = "di")]
        public string DeploymentID { get; set; }

        [Option("IISStorageAccount", AltName = "iis")]
        public CloudStorageAccount IISStorageAccount { get; set; }

        [Option("Retry", AltName = "retry")]
        public int RetryCount { get; set; }


        public override void ExecuteCommand()
        {

            while (RetryCount-- > 0)
            {
                try
                {
                    //Get the logs for the previous Hour as the current one is being used by Azure.
                    string latestLogName = "u_ex" + string.Format("{0:yyMMddHH}", DateTime.UtcNow.AddHours(-1)) + ".log";
                    DirectoryInfo info = new System.IO.DirectoryInfo(Path.Combine(Environment.CurrentDirectory, latestLogName));
                    if (!Directory.Exists(info.FullName))
                    {
                        Directory.CreateDirectory(info.FullName);
                    }                    
                    string standardError = string.Empty;
                    string standardOutput = string.Empty;
                    for (int i = 0; i < 9;i++ )  //download log for up to 9 instances. TBD : Need to update this to dynamically get the current instance count. It would still be flaky as there is a log in uploading IIS logs and when we download them.
                        ReportHelpers.DownloadBlobToLocalFile(IISStorageAccount, DeploymentID + "/NuGetGallery/NuGetGallery_IN_0/Web/W3SVC1273337584/" + latestLogName, Path.Combine(info.FullName, "IN" + i.ToString()+".log"), "wad-iis-requestlogs");
                    
                    int exitCode = InvokeNugetProcess(@"-i:IISW3C -o:CSV " + @"""" + String.Format(@"select count(*) from {0}\*.log", info.FullName) + @"""" + " -stats:OFF", out standardError, out standardOutput);
                    Console.WriteLine(exitCode);
                    Console.WriteLine(standardOutput);
                    string requestCount = standardOutput.Replace("COUNT(ALL *)", "");
                    requestCount = requestCount.Substring(2, requestCount.IndexOf(Environment.NewLine,2)).Trim();
                    Tuple<string, string> datapoint = new Tuple<string, string>(string.Format("{0:HH:00}", DateTime.Now.AddHours(-1)), requestCount);
                    string blobName = "IISRequests" + string.Format("{0:MMdd}", DateTime.Now.AddHours(-1)) + ".json";
                    ReportHelpers.AppendDatatoBlob(StorageAccount, blobName, datapoint, 50, ContainerName);
                    break; // break if the operation succeeds without doing any retry.
                }catch(Exception e)
                {
                    Console.WriteLine(string.Format("Exception thrown while trying to create report : {0}",e.Message));
                }
            }
        }


        private static int InvokeNugetProcess(string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
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
            standardOutput = nugetProcess.StandardOutput.ReadToEnd();
            nugetProcess.WaitForExit();
            return nugetProcess.ExitCode;
        }



    }
}

