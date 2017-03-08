using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Web.Script.Serialization;
using System.Web.UI;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;


namespace NuGetGallery.Operations
{
    [Command("SendLoadTestReportEmailTask", "Creates summary report for various metrics during given duration", AltName = "sltret")]
    public class SendLoadTestReportEmailTask : StorageTask
    {
        [Option("StartTime", AltName = "start")]
        public int StartTime { get; set; }

        [Option("EndTime", AltName = "end")]
        public int EndTime { get; set; }

        [Option("Date", AltName = "date")]
        public string Date { get; set; }

              
        public int LoadTestRequestPerHour { get; set; }

        private HtmlTextWriter metricWriter = new HtmlTextWriter(new StringWriter());
        private HtmlTextWriter scenarioWriter = new HtmlTextWriter(new StringWriter());
        private string prodContainerName = "test";
        private string intContainerName = "int0";
        public override void ExecuteCommand()
        {
            string dateSuffix = Date;          
            AddMetricToReport("DB Requests", GetMetricValuesForInt("DBRequests" + dateSuffix + ".json"), 25);
            AddMetricToReport("DB Connections", GetMetricValuesForInt("DBConnections" + dateSuffix + ".json"), 50);
            AddMetricToReport("DB Suspended Requests", GetMetricValuesForInt("DBSuspendedRequests" + dateSuffix + ".json"), 10);          
            AddMetricToReport("Gallery Instance Count", GetMetricValuesForInt("nuget-int-0-v2galleryInstanceCount" + dateSuffix + "HourlyReport.json"), 3);            

            List<Tuple<string,string,double>> scenarios = GetScenarioValuesFromBlob("IISRequestDetails" + dateSuffix + ".json", intContainerName, StartTime, EndTime);
            foreach (Tuple<string, string, double> scenario in scenarios)
                AddScenarioToReport(scenario, 150);
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
            return values;
        }

        private List<Tuple<string,string,double>> GetScenarioValuesFromBlob(string blobName, string containerName, int startTime, int endTime)
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
            List<Tuple<string,string,double>> scenarios = new List<Tuple<string, string,double>>();
            foreach(IGrouping<string,IISRequestDetails> group in requestGroups)
            {
                if (group.Key.Contains("Over all requests"))
                {
                    LoadTestRequestPerHour = Convert.ToInt32((group.Average(item => item.RequestsPerHour)));
                    continue;
                }
                scenarios.Add(new Tuple<string, string, double>(group.Key, Convert.ToInt32((group.Average(item => item.RequestsPerHour)/LoadTestRequestPerHour)*100) + "%", group.Average(item => item.AvgTimeTakenInMilliSeconds)));
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

        private void AddScenarioToReport(Tuple<string, string, double> scenario, double prodAvg)
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
            string duration = (EndTime - StartTime).ToString();
            duration = duration.Insert(duration.Length-2,":");
            mailBody = mailBody.Replace("{Duration}",duration + "hours");
            mailBody = mailBody.Replace("{rph}", (LoadTestRequestPerHour / 1000).ToString());
            mailBody = mailBody.Replace("{Metrics}", metricWriter.InnerWriter.ToString());
            mailBody = mailBody.Replace("{Scenarios}", scenarioWriter.InnerWriter.ToString());
            
            return mailBody;
        }      
                
    }
}

