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
        private HashSet<string> error;
        private int ErrorCount;
        private int Successed;

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
                error = new HashSet<string>();
                ErrorCount = 0;
                Successed = 0;
                double frequency = 0.0;
                bool alert = false;
                var allblobs = container.ListBlobs(prefix: task.Prefix, useFlatBlobListing: true, blobListingDetails: BlobListingDetails.None).OrderByDescending(e => (e as CloudBlockBlob).Name);
                foreach (var blob in allblobs)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        Console.WriteLine((blob as CloudBlockBlob).Name);
                        (blob as CloudBlockBlob).DownloadToStream(memoryStream);
                        StreamReader sr = new StreamReader(memoryStream);
                        sr.BaseStream.Seek(0, SeekOrigin.Begin);
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (frequency == 0)
                            {
                                if (line.Contains("SleepDuration"))
                                {
                                    int index = line.IndexOf(@"'");
                                    int end = line.IndexOf(@"'", index + 1);
                                    frequency = Convert.ToDouble(line.Substring(index + 1, end - index - 1)) / (1000 * 60);
                                    if ((blob as CloudBlockBlob).Properties.LastModified.Value.DateTime < DateTime.UtcNow.AddMinutes(-frequency * 10))
                                        alert = true;
                                }
                            }
                            if (line.Contains("[Err]"))
                            {
                                error.Add(line);
                                ErrorCount += 1;
                            }
                            if (line.Contains("Job Succeeded")) Successed += 1;
                        }
                    }
                    if (Successed + ErrorCount > 20) break;
                }
                AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));

                if (Successed != 0 && ErrorCount*100 / Successed > thresholdValues.WorkJobErrorThreshold)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = string.Format("Error: Alert for work job service : {0} failure", task.Prefix.Replace(@"\","")),
                        Details = string.Format("Rate of failure exceeded Error threshold for {0}. Threshold count : {1}%, failure in last 20 runs : {2}, error detail is {3}", task.Prefix.Replace(@"\", ""), thresholdValues.WorkJobErrorThreshold, ErrorCount,error.ToString()),
                        AlertName = string.Format("Error: Work job service {0}", task.Prefix.Replace(@"\", "")),
                        Component = "work job service",
                        Level = "Error"
                    }.ExecuteCommand();
                }
                if (alert)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = string.Format("Error: Alert for work job service : {0} failure", task.Prefix.Replace(@"\", "")),
                        Details = string.Format("worker job: {0} didn't run in last {1} min", task.Prefix.Replace(@"\", ""), frequency*10),
                        AlertName = string.Format("Error: Work job service {0}", task.Prefix.Replace(@"\", "")),
                        Component = "work job service",
                        Level = "Error"
                    }.ExecuteCommand();
                }
            }


        }
    }
}
