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
using System.Net.Mail;
using System.Web.Helpers;
using System.Web.UI;
using System.Net.Mime;
using System.Configuration;

namespace NuGetGallery.Operations
{
    [Command("CreateElmahErrorReportTask", "Creates error report from Elmah logs", AltName = "ceert")]
    public class SendAlertMailTask : OpsTask 
    {
        [Option("ErrorDetails", AltName = "e")]
        public string Details { get; set; }

        [Option("AlertSubject", AltName = "s")]
        public string AlertSubject { get; set; }

        [Option("AlertName", AltName = "s")]
        public string AlertName { get; set; }
        
        [Option("Component", AltName = "c")]
        public string Component { get; set; }


        public override void ExecuteCommand()
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
            message.From = new MailAddress(ConfigurationManager.AppSettings["SmtpUserName"], "NuGet Gallery Live site monitor");
            message.To.Add(new MailAddress(ConfigurationManager.AppSettings["MailRecepientAddress"], ConfigurationManager.AppSettings["MailRecepientAddress"]));          
            message.Subject = string.Format("[NuGet Gallery LiveSite Monitoring]: {0}",AlertSubject);
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
            StreamReader sr = new StreamReader("DashboardAlertMail.htm");
            string mailBody = sr.ReadToEnd();
            sr.Close();
            mailBody = mailBody.Replace("{AlertSubjectLine}", AlertSubject);
            mailBody = mailBody.Replace("{ComponentName}", Component);
            mailBody = mailBody.Replace("{Alert}", AlertName);
            mailBody = mailBody.Replace("{AlertDescription}", Details);
            mailBody = mailBody.Replace("{AlertTime}", DateTime.Now.ToString());
            return mailBody;

          
        }      

    }
}
