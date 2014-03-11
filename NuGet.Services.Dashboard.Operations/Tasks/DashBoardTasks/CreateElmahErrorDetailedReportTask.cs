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
            CriticalErrorDictionary.Add("The semaphore timeout period has expired",30);
            CriticalErrorDictionary.Add("The wait operation timed out",60);

            TableErrorLog log = new TableErrorLog(string.Format(ElmahAccountCredentials));
            List<ErrorLogEntry> entities = new List<ErrorLogEntry>();
            
            log.GetErrors(0, 1000, entities);
             List<string> listOfErrors = new List<string>();

            //Get the error from Last N hours.
            if (entities.Any(entity => entity.Error.Time.ToUniversalTime() > DateTime.Now.Subtract(new TimeSpan(LastNHours,0,0)).ToUniversalTime()))
            {
             entities = entities.Where(entity => entity.Error.Time.ToUniversalTime() > DateTime.Now.Subtract(new TimeSpan(LastNHours,0,0)).ToUniversalTime()).ToList();
             var elmahGroups = entities.GroupBy(item => item.Error.Message);                      
           
            //Group the error based on exception and send alerts if critical errors exceed the thresold values.
            foreach (IGrouping<string, ErrorLogEntry> errorGroups in elmahGroups)
            {
                Console.WriteLine(errorGroups.Key.ToString() + "  " + errorGroups.Count());
                listOfErrors.Add(errorGroups.Key.ToString() + "~" + errorGroups.Count().ToString() + "~" + errorGroups.Max( item => item.Error.Time.ToUniversalTime()) + "~" +  errorGroups.First().Error.Detail);
                if (CriticalErrorDictionary.ContainsKey(errorGroups.Key.ToString()))
                {
                    int countThreshold = 0;
                    CriticalErrorDictionary.TryGetValue(errorGroups.Key,out countThreshold);
                    if(errorGroups.Count() > countThreshold)
                    {
                     new SendAlertMailTask {
                    AlertSubject = "Elmah Error Alert",
                    ErrorDetails = errorGroups.Key.ToString(),
                    Count = errorGroups.Count().ToString(),
                    AdditionalLink = ""                    
                }.ExecuteCommand();
                    }
                }
            }
            }

            JArray reportObjectElmah = ReportHelpers.GetJsonForTable(listOfErrors);
            ReportHelpers.CreateBlob(StorageAccount, "ElmahErrorsDetailed" + LastNHours.ToString() + "hours.json", "dashboard", "application/json", ReportHelpers.ToStream(reportObjectElmah));
          
        }
        
        private Dictionary<string, int> CriticalErrorDictionary = new Dictionary<string, int>();

    }
}
