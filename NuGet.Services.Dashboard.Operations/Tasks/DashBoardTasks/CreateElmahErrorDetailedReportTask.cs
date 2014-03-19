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
using NuGet.Services.Dashboard.Common;


namespace NuGetGallery.Operations
{
    [Command("CreateElmahErrorDetailedReportTask", "Creates detailed error report for last N hours from Elmah logs", AltName = "ceedrt")]
    public class CreateElmahErrorDetailedReportTask : StorageTask
    {
        [Option("LastNHours", AltName = "n")]
        public int LastNHours { get; set; }

        [Option("ElmahAccountCredentials", AltName = "ea")]
        public string ElmahAccountCredentials { get; set; }
             
        public override void ExecuteCommand()
        {
            Dictionary<string, string> CriticalErrorDictionary = ReportHelpers.GetDictFromBlob(StorageAccount, "Configuration.ElmahCriticalErrors.json");

            TableErrorLog log = new TableErrorLog(string.Format(ElmahAccountCredentials));
            List<ErrorLogEntry> entities = new List<ErrorLogEntry>();
            
            log.GetErrors(0, 100 * LastNHours, entities); //retrieve n * LastNHours errors assuming a max of 50 errors per hour.
             List<ElmahError> listOfErrors = new List<ElmahError>();

            //Get the error from Last N hours.
            if (entities.Any(entity => entity.Error.Time.ToUniversalTime() > DateTime.Now.Subtract(new TimeSpan(LastNHours,0,0)).ToUniversalTime()))
            {
             entities = entities.Where(entity => entity.Error.Time.ToUniversalTime() > DateTime.Now.Subtract(new TimeSpan(LastNHours,0,0)).ToUniversalTime()).ToList();
             var elmahGroups = entities.GroupBy(item => item.Error.Message);                      
           
            //Group the error based on exception and send alerts if critical errors exceed the thresold values.
            foreach (IGrouping<string, ErrorLogEntry> errorGroups in elmahGroups)
            {
                Console.WriteLine(errorGroups.Key.ToString() + "  " + errorGroups.Count());              
                int severity = 1;
                if (CriticalErrorDictionary.Keys.Any(item => errorGroups.Key.ToString().Contains(item)))
                {
                    severity = 0;
                }
                string link = "https://www.nuget.org/Admin/Errors.axd/detail?id={0}";
                if(ContainerName.Contains("qa"))
                {
                    link = "https://int.nugettest.org/Admin/Errors.axd/detail?id={0}";
                }
                listOfErrors.Add(new ElmahError(errorGroups.Key.ToString(),errorGroups.Count(), errorGroups.Max( item => item.Error.Time.ToLocalTime()),string.Format(link,errorGroups.First().Id),errorGroups.First().Error.Detail,severity));            
                if (severity == 0)
                {
                    string countThreshold = string.Empty;
                    CriticalErrorDictionary.TryGetValue(errorGroups.Key,out countThreshold);
                    if(errorGroups.Count() > (Convert.ToInt32(countThreshold)) && LastNHours == 1)
                    {
                     new SendAlertMailTask {

                    AlertSubject = string.Format("Elmah Error Alert activated for {0}",errorGroups.Key.ToString()),
                    Details = String.Format("Number of {0} exceeded threshold limit during the last hour.Threshold per hour : {1}, Events recorded in the last hour: {2}" ,errorGroups.Key.ToString(),countThreshold.ToString(),errorGroups.Count().ToString()),
                    AlertName = string.Format("Elmah Error Alert for {0}",errorGroups.Key.ToString()),
                    Component = "Web Server"                                      
                }.ExecuteCommand();
                    }
                }            
            }
            }

            var json = new JavaScriptSerializer().Serialize(listOfErrors);          
            ReportHelpers.CreateBlob(StorageAccount, "ElmahErrorsDetailed" + LastNHours.ToString() + "hours.json", ContainerName, "application/json", ReportHelpers.ToStream(json));
          
        }      
  

    }
}
