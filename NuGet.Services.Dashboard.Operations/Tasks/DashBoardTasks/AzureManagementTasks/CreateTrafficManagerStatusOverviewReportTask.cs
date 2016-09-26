using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Xml;


namespace NuGetGallery.Operations
{
    [Command("CreateTrafficManagerStatusOverviewReportTask", "Creates report for highlevel status of Traffic manager", AltName = "ctmort")]
    public class CreateTrafficManagerStatusOverviewReportTask : StorageTask
    {
        [Option("SubsciptionId", AltName = "id")]
        public string SubscriptionId { get; set; }

        [Option("ProfileName", AltName = "name")]
        public string ProfileName { get; set; }

        [Option("CertificateName", AltName = "cername")]
        public string CertificateName { get; set; }
        public override void ExecuteCommand()
        {
            X509Certificate cert = X509Certificate.CreateFromCertFile(CertificateName);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://management.core.windows.net/{0}/services/WATM/profiles/{1}/definitions", SubscriptionId, ProfileName));
            request.ClientCertificates.Add(cert);
            request.Headers.Add("x-ms-version: 2014-02-01");
            request.PreAuthenticate = true;
            request.Method = "GET";
            var response = request.GetResponse();
            //Schema of the response would be as specified in http://msdn.microsoft.com/en-us/library/azure/hh758251.aspx
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseContent = reader.ReadToEnd();
                Console.WriteLine(responseContent);

                var responseDoc = new XmlDocument();
                responseDoc.LoadXml(responseContent);
                var endpointNodes = responseDoc.GetElementsByTagName("Endpoint", "http://schemas.microsoft.com/windowsazure");
                //Endpoint Structure
                //<Endpoint >
                //    <DomainName>    Name                </DomainName>
                //    <Status>        Enabled             </Status>
                //    <Type>          CloudService        </Type>
                //    <Location>      North Central US    </Location>
                //    <MonitorStatus> Online              </MonitorStatus>
                //    <Weight>        1                   </Weight>
                //</Endpoint>

                var endpointValues = new List<Tuple<string, string>>();
                foreach (XmlNode endpointNode in endpointNodes)
                {
                    string endpointName = endpointNode["DomainName"].InnerText;
                    string endpointStatus = endpointNode["MonitorStatus"].InnerText;
                    Console.WriteLine(string.Format("Endpoint name {0}, status {1}", endpointName, endpointStatus));
                    endpointValues.Add(Tuple.Create(endpointName, endpointStatus));
                    if (!endpointStatus.Equals("Online", StringComparison.OrdinalIgnoreCase))
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Error: Traffic manager endpoint alert activated for {0}", endpointName),
                            Details = string.Format("The status of the endpoint {0} monitoring by traffic manager {1} is {2}", endpointName, ProfileName, endpointStatus),
                            AlertName = "Error: Alert for TrafficManagerEndpoint",
                            Component = "TrafficManager",
                            Level = "Error"
                        }.ExecuteCommand();
                    }

                }
                JArray reportObject = ReportHelpers.GetJson(endpointValues);
                ReportHelpers.CreateBlob(StorageAccount, "TrafficManagerStatus.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
            }
        }



    }
}

