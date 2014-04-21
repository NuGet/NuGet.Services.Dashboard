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
    [Command("createpingdomweeklyreport", "Creates report for average response time for all the pingdom checks for fhe last 7 days/hours.", AltName = "cpdwr")]
    public class CreatePingdomWeeklyAndHourlyReportTask : StorageTask
    {
        [Option("PingdomUserName", AltName = "user")]
        public string UserName { get; set; }

        [Option("PingdomUserpassword", AltName = "password")]
        public string Password { get; set; }

        [Option("PingdomAppKey", AltName = "appkey")]
        public string AppKey { get; set; }

        [Option("Frequency", AltName = "f")]
        public string Frequency { get; set; }


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
                    List<Tuple<string, string>> summary = GetCheckSummaryAvgForSpecifiedTimeSpan(o["id"]);
                    JArray reportObject = ReportHelpers.GetJson(summary);
                    string checkAlias = o["name"].ToString();
                    checkAlias = checkAlias.Replace(" ",".");
                    checkAlias = checkAlias.Replace("(", "").Replace(")", "");
                    ReportHelpers.CreateBlob(StorageAccount, checkAlias + Frequency + "Report.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));              
                }
            }
        }

        private List<Tuple<string, string>> GetCheckSummaryAvgForSpecifiedTimeSpan(int checkId)
        {
            int i = 7;
            List<Tuple<string, string>> summaryValues = new List<Tuple<string, string>>();
            while (i >= 1)
            {
                //Get the average response time for the past 7 hours/weeks based on the frequency.
                long fromTime = 0;
                long toTime = 0;
                if (Frequency.Equals("Hourly", StringComparison.OrdinalIgnoreCase))
                {
                    fromTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.UtcNow.Subtract(new TimeSpan(0, i, 0, 0)));
                    toTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.UtcNow.Subtract(new TimeSpan(0, i-1, 0, 0)));
                }
                else
                {
                    fromTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.UtcNow.Subtract(new TimeSpan(i, 0, 0, 0)));
                    toTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.UtcNow.Subtract(new TimeSpan(i - 1, 0, 0, 0)));
                }
                NetworkCredential nc = new NetworkCredential(UserName, Password);
                WebRequest request = WebRequest.Create(string.Format("https://api.pingdom.com/api/2.0/summary.average/{0}?from={1}&to={2}", checkId, fromTime, toTime));
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
                                if(Frequency.Equals("Hourly",StringComparison.OrdinalIgnoreCase))
                                summaryValues.Add(new Tuple<string, string>(String.Format("{0:HH:mm}", UnixTimeStampUtility.DateTimeFromUnixTimestampSeconds(fromTime).ToLocalTime()), status.Value.ToString()));
                                else
                                    summaryValues.Add(new Tuple<string, string>(String.Format("{0:MM/dd}", UnixTimeStampUtility.DateTimeFromUnixTimestampSeconds(fromTime).ToLocalTime()), status.Value.ToString()));
                            }
                        }
                    }
                }
                i--;
            }
            return summaryValues;
        }
    }
}