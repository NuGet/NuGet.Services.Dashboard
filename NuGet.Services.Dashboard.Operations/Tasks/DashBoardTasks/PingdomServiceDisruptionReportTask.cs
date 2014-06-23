using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("PingdomServiceDisruptionReportTask", "Creates report for the outage of all micro service every hour", AltName = "psdrt")]
    class PingdomServiceDisruptionReportTask : StorageTask
    {
        [Option("PingdomUserName", AltName = "user")]
        public string UserName { get; set; }

        [Option("PingdomUserpassword", AltName = "password")]
        public string Password { get; set; }

        [Option("PingdomAppKey", AltName = "appkey")]
        public string AppKey { get; set; }

        [Option("LastNhour", AltName = "n")]
        public int LastNhour { get; set; }

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
                    string checkAlias = o["name"].ToString();
                    checkAlias = checkAlias.Replace(" ", ".");
                    checkAlias = checkAlias.Replace("(", "").Replace(")", "");
                    GetMicroServiceReportForCheck(checkAlias, o["id"]);
                    
                }
            }
        }

        private void GetMicroServiceReportForCheck(string checkAlias, int CheckId)
        {
            DateTime startingTime = DateTime.Now.AddHours(-LastNhour);
            List<Tuple<string, string>> summaryValues = new List<Tuple<string, string>>();
            string serviceStatus = "up";
            long fromTime = UnixTimeStampUtility.GetUnixTimestampSeconds(startingTime.ToUniversalTime());
            long toTime = UnixTimeStampUtility.GetUnixTimestampSeconds(DateTime.Now.ToUniversalTime());
            NetworkCredential nc = new NetworkCredential(UserName, Password);
            WebRequest request = WebRequest.Create(string.Format("https://api.pingdom.com/api/2.0/summary.outage/{0}?from={1}&to={2}", CheckId, fromTime, toTime));
            request.Credentials = nc;
            request.Headers.Add(AppKey);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            int downCount = 0;
            AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var summaryObject = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var summary in summaryObject["summary"])
                {
                    foreach (var states in summary.Value)
                    {
                        if (states["status"] == "down")
                        {
                            DateTime start = UnixTimeStampUtility.DateTimeFromUnixTimestampSeconds(states["timefrom"]).ToLocalTime();
                            DateTime end = UnixTimeStampUtility.DateTimeFromUnixTimestampSeconds(states["timeto"]).ToLocalTime();

                            
                            int downtime = (int)end.Subtract(start).TotalSeconds ;
                            if (downtime > thresholdValues.PingdomServiceDistruptionErrorThresholdInSeconds)
                            {
                                serviceStatus = "down";
                                downCount++;
                            }
                         }
                    } 
                }
            }
            if (serviceStatus.Equals("down"))
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Error: Alert for {0} pingdom service Down", checkAlias),
                    Details = string.Format("Pingdom service {0} down time exceeded Error threshold: {1} second, in last {2} hours, there are {3} down happened", checkAlias, thresholdValues.PingdomServiceDistruptionErrorThresholdInSeconds, LastNhour , downCount),
                    AlertName = string.Format("Error: Pingdom Micro Service: {0}",checkAlias),
                    Component = "Pingdom Service",
                    Level = "Error"
                }.ExecuteCommand();
            }
            ReportHelpers.AppendDatatoBlob(StorageAccount, checkAlias + string.Format("{0:MMdd}", DateTime.Now) + "outageReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), serviceStatus), 24, ContainerName);

        }
    }
}
