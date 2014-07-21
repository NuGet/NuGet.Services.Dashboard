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
using AnglicanGeek.DbExecutor;
using System;
using System.Net;
using System.Web.Script.Serialization;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using Elmah;
using NuGet.Services.Dashboard.Common;
using System.Net.Mail;
using System.Configuration;
using System.Net.Mime;

namespace NuGet.Services.Dashboard.Common
{
    
    public class RefreshElmahError
    {
        private string ElmahAccountCredentials { get; set; }
        private int LastNHours { get; set; }
        private CloudStorageAccount StorageAccount { get; set; }
        private string ContainerName { get; set; }
        private string ConnectionString { get; set; }

        public RefreshElmahError(string ConnectionString, string ContainerName, int LastNHours, string ElmahAccountCredentials)
        {
            this.ConnectionString = ConnectionString;
            this.ContainerName = ContainerName;
            this.LastNHours = LastNHours;
            this.ElmahAccountCredentials = ElmahAccountCredentials;
        }

        public RefreshElmahError(CloudStorageAccount StorageAccount, string ContainerName, int LastNHours, string ElmahAccountCredentials)
        {
            this.StorageAccount = StorageAccount;
            this.ContainerName = ContainerName;
            this.LastNHours = LastNHours;
            this.ElmahAccountCredentials = ElmahAccountCredentials;
        }




        public List<ElmahError> ExecuteRefresh()
        {
        return GetElmahError(DateTime.Now.Subtract(new TimeSpan(LastNHours, 0, 0)), DateTime.Now);
        }

        public List<ElmahError> GetElmahError(DateTime start, DateTime end)
        {
            if (StorageAccount == null) StorageAccount = CloudStorageAccount.Parse(ConnectionString);
            List<string> nonCriticalErrorDictionary = new JavaScriptSerializer().Deserialize<List<string>>(Load(StorageAccount, "Configuration.ElmahNonCriticalErrors.json", ContainerName));
            TableErrorLog log = new TableErrorLog(string.Format(ElmahAccountCredentials));
            List<ErrorLogEntry> entities = new List<ErrorLogEntry>();

            int lasthours = DateTime.Now.Subtract(start).Hours + 1;

            log.GetErrors(0, 500 * lasthours, entities); //retrieve n * LastNHours errors assuming a max of 500 errors per hour.
            List<ElmahError> listOfErrors = new List<ElmahError>();

            //Get the error from Last N hours.
            if (entities.Any(entity => entity.Error.Time.ToUniversalTime() > start.ToUniversalTime() && entity.Error.Time.ToUniversalTime() < end.ToUniversalTime()))
            {
                entities = entities.Where(entity => entity.Error.Time.ToUniversalTime() > start.ToUniversalTime() && entity.Error.Time.ToUniversalTime() < end.ToUniversalTime()).ToList();
                var elmahGroups = entities.GroupBy(item => item.Error.Message);

                //Group the error based on exception and send alerts if critical errors exceed the thresold values.
                foreach (IGrouping<string, ErrorLogEntry> errorGroups in elmahGroups)
                {
                    Console.WriteLine(errorGroups.Key.ToString() + "  " + errorGroups.Count());
                    int severity = 0;
                    if (nonCriticalErrorDictionary.Any(item => errorGroups.Key.ToString().Contains(item)))
                    {
                        severity = 1; //sev 1 is low pri and sev 0 is high pri.
                    }
                    string link = "https://www.nuget.org/Admin/Errors.axd/detail?id={0}";
                    if (ContainerName.Contains("qa"))
                    {
                        link = "https://int.nugettest.org/Admin/Errors.axd/detail?id={0}";
                    }
                    //for severity, assume all refresh error, severity = 0
                    listOfErrors.Add(new ElmahError(errorGroups.Key.ToString(), errorGroups.Count(), errorGroups.Min(item => item.Error.Time.ToLocalTime()), errorGroups.Max(item => item.Error.Time.ToLocalTime()), string.Format(link, errorGroups.First().Id), errorGroups.First().Error.Detail, severity));

                }
            }

            return listOfErrors;
        }

        private string Load(CloudStorageAccount storageAccount, string name, string containerName = "dashboard")
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            string content = string.Empty;
            if (blob != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    blob.DownloadToStream(memoryStream);
                    content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                }
            }

            return content;
        }

        
          
    }
    
}
