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
using System;
using System.Net;
using System.Web.Script.Serialization;
using NuGetGallery.Infrastructure;
using System.Web.UI;
using System.Net.Mail;
using System.Net.Mime;
using System.Configuration;
using NuGet.Services.Dashboard.Common;


namespace NuGetGallery.Operations
{
    [Command("SendLoadTestReportEmailTask", "Creates summary report for various metrics during given duration", AltName = "sltret")]
    public class SendLoadTestReportEmailTask : StorageTask
    {
        [Option("StartTime", AltName = "start")]
        public int StartTime { get; set; }

        [Option("EndTime", AltName = "end")]
        public int EndTime { get; set; }

        [Option("RequestsPerHour", AltName = "rph")]
        public int LoadTestRequestPerHour { get; set; }

        private HtmlTextWriter metricWriter = new HtmlTextWriter(new StringWriter());
        private HtmlTextWriter scenarioWriter = new HtmlTextWriter(new StringWriter());
        private string prodContainerName = "test";
        private string intContainerName = "int0";
        public override void ExecuteCommand()
        {           
            string dateSuffix = String.Format("{0:MMdd}", DateTime.Now);
            string dateSuffixForPreviousDay = String.Format("{0:MMdd}", DateTime.Now.AddDays(-1));
            AddMetricToReport("DB Requests", GetMetricValuesForInt("DBRequests" + dateSuffix + ".json"), GetMetricValuesForProd("DBRequests" + dateSuffixForPreviousDay + ".json").Average());
            AddMetricToReport("DB Connections", GetMetricValuesForInt("DBConnections" + dateSuffix + ".json"), GetMetricValuesForProd("DBConnections" + dateSuffixForPreviousDay + ".json").Average());
            AddMetricToReport("DB Suspended Requests", GetMetricValuesForInt("DBSuspendedRequests" + dateSuffix + ".json"), GetMetricValuesForProd("DBSuspendedRequests" + dateSuffixForPreviousDay + ".json").Average());
            AddMetricToReport("IISRequests", GetMetricValuesForInt("IISRequests" + dateSuffix + ".json"), GetMetricValuesForProd("IISRequests" + dateSuffixForPreviousDay + ".json").Average());
            AddMetricToReport("Gallery Instance Count", GetMetricValuesForInt("nuget-int-0-v2galleryInstanceCount" + dateSuffix + "HourlyReport.json"), GetMetricValuesForProd("nuget-prod-0-v2galleryInstanceCount" + dateSuffixForPreviousDay + "HourlyReport.json").Average());
            AddMetricToReport("Package Restore Response Time In Millisec", GetMetricValuesForInt("package.restore.downloadHourlyReport.json"), GetMetricValuesForProd("package.restore.downloadHourlyReport.json").Average());

            List<Tuple<string,double,double>> scenarios = GetScenarioValuesFromBlob("IISRequestDetails" + dateSuffix + ".json", intContainerName, StartTime, EndTime);
            foreach (Tuple<string, double, double> scenario in scenarios)
                AddScenarioToReport(scenario, 0);
            SendEmail();
        }

        private List<int> GetMetricValuesForInt(string blobName)
        {
            return GetMetricValuesFromBlob(blobName, intContainerName, StartTime, EndTime);
        }

        private List<int> GetMetricValuesForProd(string blobName)
        {
            return GetMetricValuesFromBlob(blobName, prodContainerName, 0000, 2300);
        }
        private List<int> GetMetricValuesFromBlob(string blobName,string containerName,int startTime,int endTime)
        {          
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(StorageAccount,blobName , containerName);
            List<int> values = new List<int>();
            foreach (KeyValuePair<string, string> keyValuePair in dict)
            {
                int key = Convert.ToInt32(keyValuePair.Key.Replace(":", "").Replace("-", ""));

                if ((key >= startTime) && (key <= endTime))
                {
                    values.Add(Convert.ToInt32(keyValuePair.Value));
                }
            }
            Console.WriteLine("Average : {0}", values.Average());
            Console.WriteLine("Max : {0}", values.Max());
            Console.WriteLine("Min: {0}", values.Min());
            return values;
        }

        private List<Tuple<string,double,double>> GetScenarioValuesFromBlob(string blobName, string containerName, int startTime, int endTime)
        {
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(StorageAccount, blobName, containerName);
            List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
            foreach (KeyValuePair<string, string> keyValuePair in dict)
            {
                int key = Convert.ToInt32(keyValuePair.Key.Replace(":", "").Replace("-", ""));

                if ((key >= startTime) && (key <= endTime))
                {
                    requestDetails.AddRange(new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(keyValuePair.Value));
                }
            }

            var requestGroups = requestDetails.GroupBy(item => item.ScenarioName);
            List<Tuple<string,double,double>> scenarios = new List<Tuple<string, double,double>>();
            foreach(IGrouping<string,IISRequestDetails> group in requestGroups)
            {
                scenarios.Add(new Tuple<string, double, double>(group.Key, (group.Average(item => item.RequestsPerHour)/LoadTestRequestPerHour)*100, group.Average(item => item.AvgTimeTakenInMilliSeconds)));
            }
            return scenarios;
        }


        private void AddMetricToReport(string metricName,List<int> intValues,double prodValue)
        {
            metricWriter.RenderBeginTag(HtmlTextWriterTag.Tr);
            metricWriter.AddAttribute(HtmlTextWriterAttribute.Bgcolor, "LightGray");
            metricWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            metricWriter.Write(metricName);
            metricWriter.RenderEndTag();        
            metricWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            metricWriter.Write(intValues.Average());
            metricWriter.RenderEndTag();
            metricWriter.RenderBeginTag(HtmlTextWriterTag.Td);          
            metricWriter.Write(intValues.Max());
            metricWriter.RenderEndTag();
            metricWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            metricWriter.Write(intValues.Min());
            metricWriter.RenderEndTag();
            metricWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            metricWriter.Write(prodValue);
            metricWriter.RenderEndTag();  
            metricWriter.RenderEndTag();
            metricWriter.WriteLine("");
        }

        private void AddScenarioToReport(Tuple<string, double, double> scenario, double prodAvg)
        {
            scenarioWriter.RenderBeginTag(HtmlTextWriterTag.Tr);
            scenarioWriter.AddAttribute(HtmlTextWriterAttribute.Bgcolor, "LightGray");
            scenarioWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            scenarioWriter.Write(scenario.Item1);
            scenarioWriter.RenderEndTag();
            scenarioWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            scenarioWriter.Write(scenario.Item2);
            scenarioWriter.RenderEndTag();
            scenarioWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            scenarioWriter.Write(scenario.Item3);
            scenarioWriter.RenderEndTag();
            scenarioWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            scenarioWriter.Write(prodAvg);
            scenarioWriter.RenderEndTag();        
            scenarioWriter.RenderEndTag();
            scenarioWriter.WriteLine("");
        }

        private void SendEmail()
        {
            SmtpClient sc = new SmtpClient("smtphost");
            NetworkCredential nc = new NetworkCredential(ConfigurationManager.AppSettings["SmtpUserName"], ConfigurationManager.AppSettings["SmtpPassword"]);
            sc.UseDefaultCredentials = true;
            sc.Credentials = nc;
            sc.Host = "outlook.office365.com";
            sc.EnableSsl = true;
            sc.Port = 587;
            //ServicePointManager.ServerCertificateValidationCallback = delegate(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
            message.From = new MailAddress(ConfigurationManager.AppSettings["SmtpUserName"], "NuGet Gallery Load Tests");
            message.To.Add(new MailAddress(ConfigurationManager.AppSettings["MailRecepientAddress"], ConfigurationManager.AppSettings["MailRecepientAddress"]));
            message.Subject = string.Format("NuGet Gallery Weekly Load Test Report");
            message.IsBodyHtml = true;
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(@"<html><body>" + GetMailContent() + "</body></html>", new ContentType("text/html")));

            try
            {
                sc.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Error in sending mail : {0}", ex.Message);
                Console.ReadKey();
            }
        }

        private string GetMailContent()
        {
            StreamReader sr = new StreamReader(@"ScriptsAndReferences\LoadTestReport.htm");
            string mailBody = sr.ReadToEnd();
            sr.Close();
            mailBody = mailBody.Replace("{Duration}", EndTime - StartTime + "hours");
            mailBody = mailBody.Replace("{rph}", (LoadTestRequestPerHour / 1000).ToString());
            mailBody = mailBody.Replace("{Metrics}", metricWriter.InnerWriter.ToString());
            mailBody = mailBody.Replace("{Scenarios}", scenarioWriter.InnerWriter.ToString());
            
            return mailBody;
        }      
                
    }
}

