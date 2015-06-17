using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using System;
using System.Net;
using System.Web.Script.Serialization;
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
                CreateDepolymentIdReport(doc);
            }
        }

        private void CreateDepolymentIdReport(XmlDocument doc)
        {
            XmlNodeList depolymentIdNode = doc.GetElementsByTagName("PrivateID", "http://schemas.microsoft.com/windowsazure");
            string depolyId = depolymentIdNode[0].InnerText;
            var json = new JavaScriptSerializer().Serialize(depolyId);
            ReportHelpers.CreateBlob(StorageAccount, "DeploymentId_" + ServiceName +".json", ContainerName, "application/json", ReportHelpers.ToStream(json));
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
            int roleInstanceCount = roleInstanceNodes.Count;
            List<Tuple<string, string>> instanceStatuses = new List<Tuple<string, string>>();
            string[] invalidStatus = { "RestartingRole","CyclingRole", "FailedStartingRole", "FailedStartingVM", "UnresponsiveRole", "StoppedDeallocated", "Preparing" };
            int unReadyInstanceCount = 0;
            List<string> unReadyInstanceStatus = new List<string>();
            foreach(XmlNode node in roleInstanceNodes)
            {
                string instanceName = node.ChildNodes[1].InnerText;
                string instanceStatus = node.ChildNodes[2].InnerText;
                instanceStatuses.Add(new Tuple<string, string>(instanceName,instanceStatus));
                //Loist of instance status @ http://msdn.microsoft.com/en-us/library/azure/ee460804.aspx#RoleInstanceList. Only Ready and unknown are acceptable.
                if (!instanceStatus.Equals("ReadyRole", StringComparison.OrdinalIgnoreCase))
                {
                    unReadyInstanceCount++;
                    unReadyInstanceStatus.Add(instanceName + "-" + instanceStatus);
                }
                if (invalidStatus.Contains(instanceStatus))
                {
                    string extendedStatus = node.ChildNodes[3].InnerText;
                    new SendAlertMailTask
                    {
                        AlertSubject = string.Format("Cloud service {0} not in good state", ServiceName),
                        Details = string.Format("The status of the instance {0} in cloud service {1} is {2}. Addtional information on the instance statuses :  {3}", instanceName, ServiceName, instanceStatus,extendedStatus),
                        AlertName = string.Format("Cloud service {0} not in good state", ServiceName), //ensure uniqueness in Alert name as that is being used incident key in pagerduty.
                        Component = "CloudService",
                        Level = "Error"
                    }.ExecuteCommand();
                }
            }
            if((roleInstanceCount-unReadyInstanceCount) < 2) 
            {
                string unReadyReport = string.Join(",", unReadyInstanceStatus.ToArray());
                new SendAlertMailTask
                    {
                        AlertSubject = string.Format("Multiple instances in Cloud service {0} not in good state", ServiceName),
                        Details = string.Format("Multiple instances in service '{0}' not in good state. The list of instance names and statuses are as follows : {1}",  ServiceName, unReadyReport),
                        AlertName = string.Format("Multiple instances in Cloud service {0} not in good state", ServiceName), //ensure uniqueness in Alert name as that is being used incident key in pagerduty.
                        Component = "CloudService " + ServiceName,
                        Level = "Error"
                    }.ExecuteCommand();
            }

            JArray reportObject = ReportHelpers.GetJson(instanceStatuses);
            ReportHelpers.CreateBlob(StorageAccount,  ServiceName + "InstanceStatus.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
        }
    }
}

