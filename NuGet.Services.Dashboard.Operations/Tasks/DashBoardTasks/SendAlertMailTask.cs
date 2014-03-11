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

namespace NuGetGallery.Operations
{
    [Command("CreateElmahErrorReportTask", "Creates error report from Elmah logs", AltName = "ceert")]
    public class SendAlertMailTask : OpsTask 
    {
        [Option("ErrorDetails", AltName = "e")]
        public string ErrorDetails { get; set; }

        [Option("Count", AltName = "x")]
        public string Count { get; set; }

        [Option("AlertSubject", AltName = "s")]
        public string AlertSubject { get; set; }

        [Option("AdditionalLink", AltName = "l")]
        public string AdditionalLink { get; set; }

        public override void ExecuteCommand()
        {

            SmtpClient sc = new SmtpClient("smtphost");
            NetworkCredential nc = new NetworkCredential("", "");
            sc.UseDefaultCredentials = true;
            sc.Credentials = nc;
            sc.Host = "outlook.office365.com";
            sc.EnableSsl = true;
            sc.Port = 587;          
            //ServicePointManager.ServerCertificateValidationCallback = delegate(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
            message.From = new MailAddress("", "NuGet Gallery Live site monitor");
            message.To.Add(new MailAddress("", ""));          
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
            stringwriter = new StringWriter();
            htmlWriter = new HtmlTextWriter(stringwriter);
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.H3);
            htmlWriter.AddAttribute(HtmlTextWriterAttribute.Bgcolor, "Yellow");
            //htmlWriter.Write(content);
            htmlWriter.Write("The following spike is observed in {0}", AlertSubject);
            htmlWriter.RenderEndTag();
            htmlWriter.AddAttribute(HtmlTextWriterAttribute.Border, "1");
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Table);
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Tr);
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Th);
            htmlWriter.Write("Error");
            htmlWriter.RenderEndTag();
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Th);
            htmlWriter.Write("Count");
            htmlWriter.RenderEndTag();
            htmlWriter.RenderEndTag();

            //foreach (WorkItem item in bugs)
            //{
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Tr);
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            htmlWriter.Write(ErrorDetails);
            htmlWriter.RenderEndTag();
            //  htmlWriter.RenderEndTag();
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Td);
            htmlWriter.Write(Count);
            htmlWriter.RenderEndTag();

            htmlWriter.RenderEndTag();
            //}          

            htmlWriter.RenderEndTag();
            htmlWriter.WriteLine("");
            htmlWriter.AddAttribute(HtmlTextWriterAttribute.Target, "_blank");
            htmlWriter.AddAttribute(HtmlTextWriterAttribute.Href, AdditionalLink);
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.A);

            htmlWriter.Write("Click here for details");
            htmlWriter.RenderEndTag();
            return stringwriter.ToString();
        }

        private static StringWriter stringwriter;
        private static HtmlTextWriter htmlWriter;

    }
}
