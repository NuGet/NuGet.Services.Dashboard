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
    [Command("createpingdommontlyreport", "Creates report for the monthly average pingdom values", AltName = "cpdmr")]
    public class CreatePingdomMonthlyReportTask : StorageTask
    {
        [Option("PingdomUserName", AltName = "user")]
        public string UserName { get; set; }

        [Option("PingdomUserpassword", AltName = "password")]
        public string Password { get; set; }

        [Option("PingdomAppKey", AltName = "appkey")]
        public string AppKey { get; set; }

        [Option("MonthName", AltName = "m")]
        public string Month { get; set; }

        [Option("Year", AltName = "y")]
        public int Year { get; set; }


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
                    List<Tuple<string, string>> summary = GetLastMonthAvgMetrics(o["id"]);
                    JArray reportObject = ReportHelpers.GetJson(summary);
                    string checkAlias = o["name"].ToString();
                    checkAlias = checkAlias.Substring(0, checkAlias.IndexOf(" "));                    
                    ReportHelpers.CreateBlob(StorageAccount, checkAlias + Month + "MonthlyReport.json", "dashboard", "application/json", ReportHelpers.ToStream(reportObject));
                }
            }
        }       

        private List<Tuple<string,string>> GetLastMonthAvgMetrics(int checkId)
        {
            long startTime = UnixTimeStampUtility.GetUnixTimestampSeconds(new DateTime(Year, UnixTimeStampUtility.GetMonthNumber(Month), 01));
            long endTime = startTime + UnixTimeStampUtility.GetSecondsForDays(DateTime.DaysInMonth(Year, UnixTimeStampUtility.GetMonthNumber(Month)));        
          
            WebRequest request =  GetPingdomRequest(string.Format("https://api.pingdom.com/api/2.0/summary.average/{0}?includeuptime=true&from={1}&to={2}",checkId,startTime,endTime));          
            List<Tuple<string,string>> summaryValues = new List<Tuple<string,string>>();
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var summaryObject = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var summary in summaryObject["summary"])
                {
                    foreach (var status in summary.Value)
                    {
                        summaryValues.Add(new Tuple<string,string>((string)status.Key, status.Value.ToString()));                       
                    }                  
                }
            }

           request = GetPingdomRequest(string.Format("https://api.pingdom.com/api/2.0/summary.outage/{0}?from={1}&to={2}", checkId, startTime, endTime));
           respose = request.GetResponse();
           using (var reader = new StreamReader(respose.GetResponseStream()))
           {
               JavaScriptSerializer js = new JavaScriptSerializer();
               var summaryObject = js.Deserialize<PingdomMonthlyReportRootObject>(reader.ReadToEnd());
               int outages = summaryObject.summary.states.Count(item => item.status.Equals("down"));      
               summaryValues.Add(new Tuple<string,string>("Outages", outages.ToString()));
           }
           
            return summaryValues;            
        }

        private WebRequest GetPingdomRequest(string url)
        {
            NetworkCredential nc = new NetworkCredential(UserName, Password);
            WebRequest request = WebRequest.Create(url);
            request.Credentials = nc;
            request.Headers.Add(AppKey);
            request.PreAuthenticate = true;
            request.Method = "GET";
            return request;
        }
    }


    public class State
    {
        public string status { get; set; }
        public int timefrom { get; set; }
        public int timeto { get; set; }
    }

    public class Summary
    {
        public List<State> states { get; set; }
    }

    public class PingdomMonthlyReportRootObject
    {
        public Summary summary { get; set; }
    }
}