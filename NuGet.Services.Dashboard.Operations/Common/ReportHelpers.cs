using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;

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
    }
}
