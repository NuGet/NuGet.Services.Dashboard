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
using System.Security.Cryptography.X509Certificates;
using System.Xml;


namespace NuGetGallery.Operations
{
    [Command("CreateCloudServiceDetailsReportTask", "Creates the report for the details of the specified cloud service.", AltName = "ccsdrt")]
    public class CreateCloudServiceDetailsReportTask : StorageTask
    {
        [Option("SubsciptionId", AltName = "id")]
        public string SubscriptionId { get; set; }

        [Option("ServiceName", AltName = "name")]
        public string ServiceName { get; set; }

        [Option("CertificateName", AltName = "cername")]
        public string CertificateName { get; set; }


        public override void ExecuteCommand()
        {
            X509Certificate cert = X509Certificate.CreateFromCertFile(CertificateName);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production?embed-detail=true", SubscriptionId, ServiceName));
            request.ClientCertificates.Add(cert);
            request.Headers.Add("x-ms-version: 2014-02-01");        
            request.PreAuthenticate = true;
            request.Method = "GET";            
            WebResponse respose = request.GetResponse();
            //Get the instance count from the response. Schema of the response would be as specified in http://msdn.microsoft.com/en-us/library/windowsazure/gg592580.aspx
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {            
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(reader.ReadToEnd());
                CreateInstanceCountReport(doc);
                CreateInstanceStateReport(doc);              
            }
        }

        private void CreateInstanceCountReport(XmlDocument doc)        
        {
            XmlNodeList roleInstanceNodes = doc.GetElementsByTagName("RoleInstance", "http://schemas.microsoft.com/windowsazure");
            Console.WriteLine(roleInstanceNodes.Count);
            ReportHelpers.AppendDatatoBlob(StorageAccount, ServiceName + "InstanceCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), roleInstanceNodes.Count.ToString()), 24, ContainerName);           
        }
        private void CreateInstanceStateReport(XmlDocument doc)
        {
            XmlNodeList roleInstanceNodes = doc.GetElementsByTagName("RoleInstance", "http://schemas.microsoft.com/windowsazure");
            List<Tuple<string, string>> instanceStatuses = new List<Tuple<string, string>>();
            foreach(XmlNode node in roleInstanceNodes)
            {
                string instanceName = node.ChildNodes[1].InnerText;
                string instanceStatus = node.ChildNodes[2].InnerText;
                instanceStatuses.Add(new Tuple<string, string>(instanceName,instanceStatus));
                //Loist of instance status @ http://msdn.microsoft.com/en-us/library/azure/ee460804.aspx#RoleInstanceList. Only Ready and unknown are acceptable.
                if (!instanceStatus.Equals("ReadyRole",StringComparison.OrdinalIgnoreCase) && !instanceStatus.Equals("RoleStateUnknown",StringComparison.OrdinalIgnoreCase))
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = string.Format("Role Instance alert activated for {0} cloud service", ServiceName),
                        Details = string.Format("The status of the instance {0} in cloud service {1} is {2}", instanceName, ServiceName, instanceStatus),
                        AlertName = string.Format("Alert for Role Instance status for {0}",ServiceName), //ensure uniqueness in Alert name as that is being used incident key in pagerduty.
                        Component = "CloudService"
                    }.ExecuteCommand();
                }
            }
            JArray reportObject = ReportHelpers.GetJson(instanceStatuses);
            ReportHelpers.CreateBlob(StorageAccount,  ServiceName + "InstanceStatus.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
        }
    }
}

