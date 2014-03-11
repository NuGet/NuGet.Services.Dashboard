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
    [Command("createpingdomweeklyreport", "Creates report for the weekly average pingdom values", AltName = "cpdwr")]
    public class CreatePingdomWeeklyReportTask : StorageTask
    {
        [Option("PingdomUserName", AltName = "user")]
        public string UserName { get; set; }

        [Option("PingdomUserpassword", AltName = "password")]
        public string Password { get; set; }

        public override void ExecuteCommand()
        {
            NetworkCredential nc = new NetworkCredential(UserName, Password);
            WebRequest request = WebRequest.Create("https://api.pingdom.com/api/2.0/checks");
            request.Credentials = nc;
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var o in objects["checks"])
                {
                    List<Tuple<string, string>> summary = GetCheckSummaryAvgForLastWeek(o["id"]);
                    JArray reportObject = ReportHelpers.GetJson(summary);
                    string checkAlias = o["name"].ToString();
                    checkAlias = checkAlias.Substring(0, checkAlias.IndexOf(" "));
                    ReportHelpers.CreateBlob(StorageAccount, checkAlias + "WeeklyReport.json", "dashboard", "application/json", ReportHelpers.ToStream(reportObject));
                }
            }
        }

        private List<Tuple<string, string>> GetCheckSummaryAvgForLastWeek(int checkId)
        {
            int i = 8;
            List<Tuple<string, string>> summaryValues = new List<Tuple<string, string>>();
            while (i >= 1)
            {
                //Get the average response time for the past 8 days.
                long fromTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.UtcNow.Subtract(new TimeSpan(i, 0, 0, 0)));
                long toTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.UtcNow.Subtract(new TimeSpan(i - 1, 0, 0, 0)));
                NetworkCredential nc = new NetworkCredential(UserName, Password);
                WebRequest request = WebRequest.Create(string.Format("https://api.pingdom.com/api/2.0/summary.average/{0}?from={1}&to={2}", checkId, fromTime, toTime));
                request.Credentials = nc;
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
                                summaryValues.Add(new Tuple<string, string>("Day" + i.ToString(), status.Value.ToString()));
                        }
                    }
                }
                i--;
            }
            return summaryValues;
        }
    }
}