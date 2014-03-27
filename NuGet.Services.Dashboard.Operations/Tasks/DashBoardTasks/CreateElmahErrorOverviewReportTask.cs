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
            log.GetErrors(0, 500, entities); //retrieve n * LastNHours errors assuming a max of 500 errors per hour.
            int count = entities.Where(entity => entity.Error.Time.ToUniversalTime() > DateTime.UtcNow.AddHours(-1) && entity.Error.Time.ToUniversalTime() < DateTime.UtcNow).ToList().Count;
            ReportHelpers.AppendDatatoBlob(StorageAccount, "ErrorRate" + string.Format("{0:MMdd}", DateTime.Now) + ".json", new Tuple<string, string>(String.Format("{0:HH:mm}", DateTime.Now), count.ToString()), 50, ContainerName);            
        }

        

    }
}

