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

namespace NuGetGallery.Operations
{
    [Command("CreatePingdomDetailedReportTask", "Creates report for the avg res time for each hour for the last N days for the specified check Id. ", AltName = "cpdrt")]
    public class CreatePingdomDetailedReportTask : StorageTask
    {
        [Option("PingdomUserName", AltName = "user")]
        public string UserName { get; set; }

        [Option("PingdomUserpassword", AltName = "password")]
        public string Password { get; set; }

        [Option("PingdomAppKey", AltName = "appkey")]
        public string AppKey { get; set; }

        [Option("NoOfDays", AltName = "n")]
        public int NoOfDays { get; set; }

        [Option("CheckId", AltName = "id")] //the check Id for various pingdom checks can be found from status.nuget.org
        public string CheckId { get; set; }

        public override void ExecuteCommand()
        {
            NetworkCredential nc = new NetworkCredential(UserName, Password);
            WebRequest request = WebRequest.Create("https://api.pingdom.com/api/2.0/checks");
            request.Credentials = nc;
            request.Headers.Add(AppKey);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var o in objects["checks"])
                {
                    if (!o["id"].ToString().Equals(CheckId))
                        continue;
                    else
                    {
                        string checkAlias = o["name"].ToString();
                        checkAlias = checkAlias.Replace(" ", ".");
                        checkAlias = checkAlias.Replace("(", "").Replace(")", "");
                        GetDetailedReportForCheck(checkAlias);                        
                    }
                }
            }
        }

        private void GetDetailedReportForCheck(string checkAlias)
        {

            DateTime startingTime = DateTime.Now.AddHours(DateTime.Now.Hour * -1).AddDays(NoOfDays * -1); //get the midnight time for today to create separate report for each day.
            for (int j = 0; j <= NoOfDays; j++)
            {
                int i = 0;
                List<Tuple<string, string>> summaryValues = new List<Tuple<string, string>>();
                while (i <= 23) //get th values for each hour. TBD : For today, there may not be values for all 24 hours and they are being filled as zero.Need to fix it.
                {   
                    long fromTime = 0;
                    long toTime = 0;
                    
                        fromTime = UnixTimeStampUtility.GetUnixTimestampSeconds(startingTime.ToUniversalTime());
                        toTime = UnixTimeStampUtility.GetUnixTimestampSeconds(startingTime.AddHours(1).ToUniversalTime());
                    
                    NetworkCredential nc = new NetworkCredential(UserName, Password);
                    WebRequest request = WebRequest.Create(string.Format("https://api.pingdom.com/api/2.0/summary.average/{0}?from={1}&to={2}", CheckId, fromTime, toTime));
                    request.Credentials = nc;
                    request.Headers.Add(AppKey);
                    request.PreAuthenticate = true;
                    request.Method = "GET";
                    WebResponse respose = request.GetResponse();
                    using (var reader = new StreamReader(respose.GetResponseStream()))
                    {
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        var summaryObject = js.Deserialize<dynamic>(reader.ReadToEnd());
                        foreach (var summary in summaryObject["summary"])
                        {
                            foreach (var status in summary.Value)
                            {
                                //Get the average response time and store it to the JSON object.
                                if (status.Key == "avgresponse")
                                {                                   
                                  summaryValues.Add(new Tuple<string, string>(String.Format("{0:HH:mm}", UnixTimeStampUtility.DateTimeFromUnixTimestampSeconds(fromTime).ToLocalTime()), status.Value.ToString()));                                   
                                }
                            }
                        }
                    }
                    i++;
                    startingTime = startingTime.AddHours(1);
                }
                JArray reportObject = ReportHelpers.GetJson(summaryValues);
                ReportHelpers.CreateBlob(StorageAccount, checkAlias + string.Format("{0:MMdd}",startingTime.AddHours(-1)) + "DetailedReport.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
            }
        }
    }
}