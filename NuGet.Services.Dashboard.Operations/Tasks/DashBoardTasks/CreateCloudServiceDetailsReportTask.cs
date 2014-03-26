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


        public override void ExecuteCommand()
        {
            X509Certificate cert = X509Certificate.CreateFromCertFile("bhuvak-dashboardautomation.cer");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}?embed-detail=true", SubscriptionId, ServiceName));
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
                XmlNodeList parentNode = doc.GetElementsByTagName("Deployment","http://schemas.microsoft.com/windowsazure");               
                foreach(XmlNode node in parentNode)
                {
                    //get the instance count of the production slot.
                    if (!node.ChildNodes[1].InnerText.Equals("Production"))
                        continue;
                    parentNode = node.ChildNodes;
                    int count = parentNode.Item(7).ChildNodes.Count;
                    Console.WriteLine(string.Format("No of instances in production slot of {0} is {1}", ServiceName, count));
                    ReportHelpers.AppendDatatoBlob(StorageAccount, ServiceName + "InstanceCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), count.ToString()), 24, ContainerName);
                }
            }
        }
    }
}

