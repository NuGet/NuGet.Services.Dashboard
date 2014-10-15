using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;
using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;

namespace NuGetGallery.Operations.Common
{
    public static class ReportHelpers
    {
        public static string Load(CloudStorageAccount storageAccount, string name, string containerName = "dashboard")
        {            
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            string content = string.Empty;
            if (blob != null)
            {  
                using (var memoryStream = new MemoryStream())
                {
                    blob.DownloadToStream(memoryStream);
                    content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                }
            }

            return content;
        }

        public static bool IffBlobExists(CloudStorageAccount storageAccount, string name, string containerName = "dashboard")
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            return (blob.Exists());

        }

        public static Task DownloadBlobToLocalFile(CloudStorageAccount storageAccount, string blobName, string fileName, string containerName = "dashboard")
        {
            Task download = null; 
          
            if (!(File.Exists(fileName) && new FileInfo(fileName).Length > 0))
            {
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                string content = string.Empty;
                if (blob.Exists())
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        Stream target = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                        download = blob.DownloadToStreamAsync(target, null, null, null);
                        download.Wait();
                        target.Close();
                    }                   
                }
            }
            return download;
        }


        public static Stream ToStream(JToken jToken)
        {
            MemoryStream stream = new MemoryStream();
            TextWriter writer = new StreamWriter(stream);
            writer.Write(jToken.ToString());
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static Stream ToJson(Tuple<string[], List<object[]>> report)
        {
            JArray jArray = new JArray();

            foreach (object[] row in report.Item2)
            {
                JObject jObject = new JObject();

                for (int i = 0; i < report.Item1.Length; i++)
                {
                    if (row[i] != null)
                    {
                        jObject.Add(report.Item1[i], new JValue(row[i]));
                    }
                    // ELSE treat null by not defining the property in our internal JSON (aka undefined)
                }

                jArray.Add(jObject);
            }

            return ToStream(jArray);
        }

        /// <summary>
        /// Converts generic List with key value Tuples as JSON serialized object.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static JArray GetJson(IEnumerable<Tuple<string, string>> values)
        {

            Func<Tuple<string, string>, JObject> objToJson =
                o => new JObject(
                        new JProperty("key", o.Item1),
                        new JProperty("value", o.Item2));

            return new JArray(values.Select(objToJson));
        }

        /// <summary>
        /// Converts generic List with key value Tuples as JSON serialized object.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static JArray GetJsonForTable(IEnumerable<string> values)
        {

            JArray array = new JArray();
            foreach(string value in values)
            {
                object[] individualCols = value.Split(new char[] { '~' }).ToArray();
                array.Add(new JObject(new JProperty("row", individualCols)));
            }

            return array;
        }

        /// <summary>
        /// Gets the JSON data from the blob. The blobs are pre-created as key value pairs using Ops tasks.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="xValues"></param>
        /// <param name="yValues"></param>
        public static Dictionary<string, string> GetDictFromBlob(CloudStorageAccount account, string blobName,string container="dashboard")
        {

            string json = Load(account, blobName,container);
            if (json == null)
            {
                return null;
            }

            Dictionary<string, string> dict = new Dictionary<string, string>();
            JArray array = JArray.Parse(json);
            foreach (JObject item in array)
            {
                if (!dict.ContainsKey(item["key"].ToString()))
                {
                    dict.Add(item["key"].ToString(), item["value"].ToString());
                }
            }
            return dict;
        }

        /// <summary>
        /// Returns a bar chart for a given set of values.
        /// </summary>
        /// <param name="seriesSet"></param>
        /// <param name="xValues"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public static DotNet.Highcharts.Highcharts GetBarChart(List<DotNet.Highcharts.Options.Series> seriesSet, List<string> xValues, string title)
        {
            DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts(title);
            chart.InitChart(new Chart
            {
                Height = 320,
                Width = 320,
                DefaultSeriesType = ChartTypes.Column
            });

            chart.SetXAxis(new XAxis
            {
                Categories = xValues.ToArray()

            });

            chart.SetLegend(new Legend { Enabled = false });
            chart.SetSeries(seriesSet.ToArray());

            chart.SetTitle(new DotNet.Highcharts.Options.Title { Text = title.Replace("_", " ")});
            
            return chart;
        }

        /// <summary>
        /// Returns a table for a given set of values.
        /// </summary>
        /// <param name="seriesSet"></param>
        /// <param name="xValues"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public static string GetOperationsPerNuGetVersionTable(List<object> installs, List<object> restores, List<string> versions, string title)
        {
            StringBuilder sb = new StringBuilder();
            string fontOpenTag = @"<b><span style='font-size:11.0pt;font-family:'Calibri','sans-serif';color:windowtext'>";
            string fontCloseTag = @"</span></b>";
            string cellOpenTag1 = @"<td width=75 valign=top style='width:75pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";
            string cellOpenTag2 = @"<td width=175 valign=top style='width:175pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";

            sb.Append(@"<table class=MsoNormalTable border=0 cellspacing=0 cellpadding=0 style='border-collapse:collapse'>");
            sb.Append(@"<tr>" + cellOpenTag1 + fontOpenTag + "Version" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "Installs/Updates" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag +  "Restores" + fontCloseTag + "</td></tr>");
                                
            int i = 0;
            foreach (string version in versions)
            {
                sb.Append(@"<tr>" + cellOpenTag1);
                sb.Append(version);
                sb.Append(@"</td>" + cellOpenTag2);
                sb.Append(Convert.ToInt64(installs[i]).ToString("#,##0"));
                sb.Append(@"</td>" + cellOpenTag2);
                sb.Append(Convert.ToInt64(restores[i++]).ToString("#,##0"));
                sb.Append("</td></tr>");
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        /// <summary>
        /// Given a list of blob names, this function returns a list of key values and another list of aggregated values for those keys
        /// For example, combined install, install-dependency counts for 2.4
        /// </summary>
        /// <param name="blobNames">List of blob names</param>
        /// <param name="storageAccount">Storage Account to be used</param>
        /// <param name="containerName">Container that has the blobs</param>
        /// <param name="xValues">List of keys</param>
        /// <param name="yValues">List of values</param>
        public static void GetValuesFromBlobs(string[] blobNames, CloudStorageAccount storageAccount, string containerName, out List<string> xValues, out List<Object> yValues)
        {
            xValues = new List<string>();
            yValues = new List<Object>();
            Dictionary<string, object> combinedValues = new Dictionary<string, object>();

            foreach (string blobName in blobNames)
            {
                string json = ReportHelpers.Load(storageAccount, blobName, containerName);
                if (json == null)
                {
                    return;
                }

                JArray array = JArray.Parse(json);
                foreach (JObject item in array)
                {
                    string currentKey = item["key"].ToString();
                    object currentValue = item["value"];
                  
                    if (!combinedValues.ContainsKey(currentKey))
                    {
                        combinedValues.Add(currentKey, currentValue);
                    }
                    else
                    {
                        combinedValues[currentKey] = Convert.ToInt64(currentValue) + Convert.ToInt64(combinedValues[currentKey]);
                    }
                }
            }

            foreach (var item in combinedValues)
            {
                xValues.Add(item.Key);
                yValues.Add(item.Value);
            }
        }

        /// <summary>
        /// Gets the JSON data from the blob. The blobs are pre-created as key value pairs using Ops tasks.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="xValues"></param>
        /// <param name="yValues"></param>
        public static void AppendDatatoBlob(CloudStorageAccount account, string blobName, Tuple<string,string> tuple,int bufferCount,string containerName="dashboard")
        {
            List<Tuple<string, string>> dict = new List<Tuple<string, string>>();
             try
             {
                 if (ReportHelpers.IffBlobExists(account, blobName, containerName))
                 {

                     string json = Load(account, blobName, containerName);
                     if (!string.IsNullOrEmpty(json))
                     {

                         JArray array = JArray.Parse(json);
                         foreach (JObject item in array)
                         {
                             dict.Add(new Tuple<string, string>(item["key"].ToString(), item["value"].ToString()));
                         }
                     }
                 }
                }catch(StorageException e )
                {
                    Console.WriteLine(string.Format("Exception thrown while trying to load blob {0}. Exception : {1}", blobName, e.Message));
                }
                   
            
            dict.Add(tuple);
            if(dict.Count > bufferCount)
            {  
                dict.RemoveRange(0, dict.Count - bufferCount);
            }

            JArray reportObject = ReportHelpers.GetJson(dict);
            ReportHelpers.CreateBlob(account, blobName , containerName, "application/json", ReportHelpers.ToStream(reportObject));            
        }




        /// <summary>
        /// Given the storage account and container name, this method creates a blob with the specified content.
        /// </summary>
        /// <param name="account"></param>
        /// <param name="blobName"></param>
        /// <param name="containerName"></param>
        /// <param name="contentType"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static Uri CreateBlob(CloudStorageAccount account, string blobName, string containerName, string contentType, Stream content)
        {            
            CloudBlobClient blobClient = account.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

            blockBlob.Properties.ContentType = contentType;
            blockBlob.UploadFromStream(content);

            return blockBlob.Uri;
        }

        public static string CreateTableForIPDistribution(CloudStorageAccount storageAccount, string containerName, DateTime date)
        {
            StringBuilder sb = new StringBuilder();
            string fontOpenTag = @"<b><span style='font-size:11.0pt;font-family:'Calibri','sans-serif';color:windowtext'>";
            string fontCloseTag = @"</span></b>";
            string cellOpenTag1 = @"<td width=200 valign=top style='width:200pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";
            string cellOpenTag2 = @"<td width=175 valign=top style='width:175pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";

            sb.Append(@"<table class=MsoNormalTable border=0 cellspacing=0 cellpadding=0 style='border-collapse:collapse'>");
            sb.Append(@"<tr>" + cellOpenTag1 + fontOpenTag + "IP Address" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "# of Requests" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "Avg Time Taken in ms" + fontCloseTag + "</td></tr>");

            string blobName = "IISIPDetails" + string.Format("{0:yyMMdd}", date.AddDays(-1)) + ".json";
            
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(storageAccount, blobName, containerName);
            IISIPDetails requestDetails = new IISIPDetails();
           
            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    requestDetails = new JavaScriptSerializer().Deserialize<IISIPDetails>(keyValuePair.Value);

                    sb.Append(@"<tr>" + cellOpenTag1);
                    sb.Append(requestDetails.cip);
                    sb.Append(@"</td>" + cellOpenTag2);
                    sb.Append(Convert.ToInt64(requestDetails.RequestsPerHour).ToString("#,##0"));
                    sb.Append(@"</td>" + cellOpenTag2);
                    sb.Append(Convert.ToInt64(requestDetails.AvgTimeTakenInMilliSeconds).ToString("#,##0"));
                    sb.Append("</td></tr>");

                }
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        public static string CreateTableForUserAgent(CloudStorageAccount storageAccount, string containerName, DateTime date)
        {
            StringBuilder sb = new StringBuilder();
            string fontOpenTag = @"<b><span style='font-size:11.0pt;font-family:'Calibri','sans-serif';color:windowtext'>";
            string fontCloseTag = @"</span></b>";
            string cellOpenTag1 = @"<td width=200 valign=top style='width:200pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";
            string cellOpenTag2 = @"<td width=175 valign=top style='width:175pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";

            sb.Append(@"<table class=MsoNormalTable border=0 cellspacing=0 cellpadding=0 style='border-collapse:collapse'>");
            sb.Append(@"<tr>" + cellOpenTag1 + fontOpenTag + "User Agent Name" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "Request Count" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "Avg Time Taken in ms" + fontCloseTag + "</td></tr>");

            string blobName = "IISUserAgentDetails" + string.Format("{0:yyMMdd}", date.AddDays(-1)) + ".json";

            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(storageAccount, blobName, containerName);
            IISUserAgentDetails requestDetails = new IISUserAgentDetails();

            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    requestDetails = new JavaScriptSerializer().Deserialize<IISUserAgentDetails>(keyValuePair.Value);

                    sb.Append(@"<tr>" + cellOpenTag1);
                    sb.Append(requestDetails.UserAgentName);
                    sb.Append(@"</td>" + cellOpenTag2);
                    sb.Append(Convert.ToInt64(requestDetails.RequestsPerHour).ToString("#,##0"));
                    sb.Append(@"</td>" + cellOpenTag2);
                    sb.Append(Convert.ToInt64(requestDetails.AvgTimeTakenInMilliSeconds).ToString("#,##0"));
                    sb.Append("</td></tr>");

                }
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        public static string CreateTableForResponseTime(CloudStorageAccount storageAccount, string containerName, DateTime date)
        {
            StringBuilder sb = new StringBuilder();
            string fontOpenTag = @"<b><span style='font-size:11.0pt;font-family:'Calibri','sans-serif';color:windowtext'>";
            string fontCloseTag = @"</span></b>";
            string cellOpenTag1 = @"<td width=200 valign=top style='width:200pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";
            string cellOpenTag2 = @"<td width=175 valign=top style='width:175pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";

            sb.Append(@"<table class=MsoNormalTable border=0 cellspacing=0 cellpadding=0 style='border-collapse:collapse'>");
            sb.Append(@"<tr>" + cellOpenTag1 + fontOpenTag + "Uri Stem" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "Avg Time Taken in ms" + fontCloseTag + "</td></tr>");

            string blobName = "IISResponseTimeDetails" + string.Format("{0:yyMMdd}", date.AddDays(-1)) + ".json";

            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(storageAccount, blobName, containerName);
            IISResponseTimeDetails requestDetails = new IISResponseTimeDetails();

            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    requestDetails = new JavaScriptSerializer().Deserialize<IISResponseTimeDetails>(keyValuePair.Value);

                    sb.Append(@"<tr>" + cellOpenTag1);
                    sb.Append(requestDetails.UriStem);
                    sb.Append(@"</td>" + cellOpenTag2);
                    sb.Append(Convert.ToInt64(requestDetails.AvgTimeTakenInMilliSeconds).ToString("#,##0"));
                    sb.Append("</td></tr>");

                }
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        public static string CreateTableForIISRequestsDistribution(CloudStorageAccount storageAccount, string containerName, List<string> datesInWeek)
        {
            StringBuilder sb = new StringBuilder();
            string fontOpenTag = @"<b><span style='font-size:11.0pt;font-family:'Calibri','sans-serif';color:windowtext'>";
            string fontCloseTag = @"</span></b>";
            string cellOpenTag1 = @"<td width=200 valign=top style='width:200pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";
            string cellOpenTag2 = @"<td width=175 valign=top style='width:175pt;border:solid #8EAADB 1.0pt;border-bottom:solid #8EAADB 1.5pt;padding:0in 5.4pt 0in 5.4pt'>";

            sb.Append(@"<table class=MsoNormalTable border=0 cellspacing=0 cellpadding=0 style='border-collapse:collapse'>");
            sb.Append(@"<tr>" + cellOpenTag1 + fontOpenTag + "Scenario Name" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "# of Requests" + fontCloseTag + "</td>");
            sb.Append(cellOpenTag2 + fontOpenTag + "Avg Time Taken in ms" + fontCloseTag + "</td></tr>");

            var content = ReportHelpers.Load(storageAccount, "Configration.IISRequestStems.json", containerName);
            List<IISRequestDetails> UriStems = new List<IISRequestDetails>();
            UriStems = new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(content);
            foreach (IISRequestDetails stem in UriStems)
            {
                int avgTime = 0;
                int requestCount = ReportHelpers.GetQueryNumbers(stem.ScenarioName, out avgTime, datesInWeek, storageAccount, containerName);

                sb.Append(@"<tr>" + cellOpenTag1);
                sb.Append(stem.ScenarioName);
                sb.Append(@"</td>" + cellOpenTag2);
                sb.Append(Convert.ToInt64(requestCount).ToString("#,##0"));
                sb.Append(@"</td>" + cellOpenTag2);
                sb.Append(Convert.ToInt64(avgTime).ToString("#,##0"));
                sb.Append("</td></tr>");
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        public static int GetQueryNumbers(string scenarioName, out int avgTime, List<string> DatesInWeek, CloudStorageAccount storageAccount, string containerName)
        {
            List<int> queries = new List<int>();
            avgTime = 0;
            foreach (string date in DatesInWeek)
            {
                int queryNumber = GetQueryNumbersFromBlob(date, scenarioName, out avgTime, storageAccount, containerName);
                queries.Add(queryNumber);
            }
            return queries.Sum();
        }

        public static int GetQueryNumbersFromBlob(string Date, string scenarioName, out int avgTime, CloudStorageAccount storageAccount, string containerName)
        {
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(storageAccount, "IISRequestDetails" + Date + ".json", containerName);
            List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
            int totalRequestNumber = 0;
            avgTime = 0;
            int count = 0;

            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    requestDetails = new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(keyValuePair.Value);
                    foreach (IISRequestDetails detail in requestDetails)
                    {
                        if (detail.ScenarioName == scenarioName)
                        {
                            totalRequestNumber += detail.RequestsPerHour;
                            avgTime += detail.AvgTimeTakenInMilliSeconds;
                            count++;
                        }
                    }
                }
            }
            if (count > 1)
            { 
                avgTime = Convert.ToInt32(avgTime / count);
            }
            return totalRequestNumber;
        }
    }
}
