using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using NuGet.Services.Dashboard.Common;
using System.Web.Script.Serialization;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks.WorkServiceTasks
{
    [Command("CreateVMWorkJobDetailReportTask", "Create work job detail", AltName = "cvwjdrt")]
    class CreateVMWorkJobDetailReportTask : StorageTask
    {
        private StringBuilder errorFile;
        private int ErrorCount;
        private int Successed;
        private List<string> runs;

        [Option("LogStorageUri", AltName = "uri")]
        public string LogStorageUri { get; set; }

        [Option("LogStorageContainerName", AltName = "lct")]
        public string LogStorageContainerName { get; set; }

        public override void ExecuteCommand()
        {
            CloudStorageAccount LogStorageAccount = CloudStorageAccount.Parse(LogStorageUri);
            CloudBlobClient blobClient = LogStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(LogStorageContainerName);
            var blobs = container.ListBlobs(null, false, BlobListingDetails.None);
            var dirs = blobs.Where(b => b as CloudBlobDirectory != null).ToList();

            foreach (CloudBlobDirectory task in dirs)
            {
                ErrorCount = 0;
                Successed = 0;
                int len = task.Prefix.Length;
                int adjust = "yyyy/MM/dd/hh/mm/ss/xxxxxxx/NUGET-PROD-JOBS/".Length;
                string date = string.Format("{0:yyyy/MM/dd/}",DateTime.UtcNow);
                errorFile = new StringBuilder();
                runs = new List<string>();

                if (!task.Prefix.ToString().Contains("Ng"))
                { 
                    int days = 1;
                    while (true)
                    {
                        var allblobs = container.ListBlobs(prefix: task.Prefix + date, useFlatBlobListing: true, blobListingDetails: BlobListingDetails.None).OrderByDescending(e => (e as CloudBlockBlob).Name).Where(e => (e as CloudBlockBlob).Name.Substring(len + adjust).Equals("ended.txt")).Take(20);

                        int count = 0;
                        date = string.Format("{0:yyyy/MM/dd/}", DateTime.UtcNow.AddDays(-days));
                        foreach (var blob in allblobs)
                        {
                            count++;
                            bool result = EndfileCheck((blob as CloudBlockBlob).Name.ToString(),container, task.Prefix.Replace(@"\", ""));
                            
                        }
                        if (count != 0 || days > 5) break;
                        days++;
                    }
                }
                else
                {
                    CollectJobRun(task);
                    int runToday = 0;
                    foreach(string run in runs)
                    {
                        bool result = EndfileCheck(run + @"ended.txt", container, task.Prefix.Replace(@"\", ""));
                        if (run.Contains(date)) runToday++;
                    }

                    if (runToday >= 5)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Warning: Alert for VM jobs : {0} ", task.Prefix.Replace(@"\", "")),
                            Details = string.Format("{0} task have more than 5 runs today, we should check it.", task.Prefix.Replace(@"\", "")),
                            AlertName = string.Format("Error: Vm jobs {0}", task.Prefix.Replace(@"\", "")),
                            Component = "Vm jobs",
                            Level = "Error"
                        }.ExecuteCommand();
                    }            
                }
                
                AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));

                if (Successed != 0 && ErrorCount*100 / (Successed+ErrorCount) > thresholdValues.WorkJobErrorThreshold)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = string.Format("Error: Alert for VM jobs : {0} failure", task.Prefix.Replace(@"\","")),
                        Details = string.Format("{0} Rate of failure exceeded Error threshold {1}%. in last 20 runs, following run are failed: {2}", task.Prefix.Replace(@"\", ""), thresholdValues.WorkJobErrorThreshold, ErrorCount, errorFile.ToString()),
                        AlertName = string.Format("Error: Vm jobs {0}", task.Prefix.Replace(@"\", "")),
                        Component = "Vm jobs",
                        Level = "Error"
                    }.ExecuteCommand();
                }               
            }
        }

        private void CollectJobRun(CloudBlobDirectory root)
        {
            var allblobs = root.ListBlobs(useFlatBlobListing: false, blobListingDetails: BlobListingDetails.None);
            foreach (var blob in allblobs)
            {
                if (blob is CloudBlobDirectory) CollectJobRun(blob as CloudBlobDirectory);
                else
                {
                    runs.Add(root.Prefix);
                    break;
                }
            }
        }

        private bool EndfileCheck(string filename, CloudBlobContainer container, string taskName)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(filename);
            string lastlogFile = "";
            using (var memoryStream = new MemoryStream())
            {
                try
                {
                    blob.DownloadToStream(memoryStream);
                    StreamReader sr = new StreamReader(memoryStream);
                    sr.BaseStream.Seek(0, SeekOrigin.Begin);
                    lastlogFile = sr.ReadLine();
                }
                catch
                {
                    return true;
                }
            }
            CloudBlockBlob lastBlob = container.GetBlockBlobReference(lastlogFile);
            using (var memoryStream = new MemoryStream())
            {
                Console.WriteLine((blob as CloudBlockBlob).Name);
                try
                {
                    lastBlob.DownloadToStream(memoryStream);
                }
                catch
                {
                    return false;
                }

                StreamReader sr = new StreamReader(memoryStream);
                sr.BaseStream.Seek(0, SeekOrigin.Begin);
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("Job Succeeded")) return true;
                    if (line.Contains("Job Failed"))
                    {
                        errorFile.Append((blob as CloudBlockBlob).Name + ",");
                        return false;
                    }
                    if (line.Contains("Job Crashed"))
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Error: Alert for VM jobs : {0} Crashed", taskName),
                            Details = string.Format("VM {0} job crashed at {1}, the last blob file name is {2}", taskName, (blob as CloudBlockBlob).Properties.LastModified.ToString(), (blob as CloudBlockBlob).Name),
                            AlertName = string.Format("Error: Vm Jobs {0}", taskName),
                            Component = "Vm jobs",
                            Level = "Error"
                        }.ExecuteCommand();
                        return false;
                    }
                }
            }
            return false;
        }
    }
}
