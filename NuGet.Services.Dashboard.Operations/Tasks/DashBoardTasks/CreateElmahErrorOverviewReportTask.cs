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


namespace NuGetGallery.Operations
{
    [Command("CreateElmahErrorOverviewReportTask", "Creates trending report for Elmah error count", AltName = "ceeort")]
    public class CreateElmahErrorOverviewReportTask : StorageTask
    { 
             [Option("ElmahAccountCredentials", AltName = "ea")]
             public string ElmahAccountCredentials { get; set; }
        public override void ExecuteCommand()
        {           

            TableErrorLog log = new TableErrorLog(string.Format(ElmahAccountCredentials));
            List<ErrorLogEntry> entities = new List<ErrorLogEntry>();
            
            log.GetErrors(0, 500, entities);
            
            List<Tuple<string, string>> hourlyErrorCounts = new List<Tuple<string, string>>();
            DateTime startingTime = DateTime.Now.Subtract(new TimeSpan(5,0,0)).ToUniversalTime();
            while (startingTime <= DateTime.Now.ToUniversalTime())
            {
                //For the last 5 hours, retrieves the error count each hour. 
                int count = entities.Where(entity => entity.Error.Time.ToUniversalTime() > startingTime && entity.Error.Time.ToUniversalTime() < startingTime.AddHours(1)).ToList().Count;
                hourlyErrorCounts.Add(new Tuple<string, string>(String.Format("{0:HH:mm}", startingTime.ToLocalTime()), count.ToString()));
                startingTime = startingTime.AddHours(1);
            }                     

            JArray reportObject = ReportHelpers.GetJson(hourlyErrorCounts);
            ReportHelpers.CreateBlob(StorageAccount, "ErrorRate" + ".json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
          
        }

        

    }
}

