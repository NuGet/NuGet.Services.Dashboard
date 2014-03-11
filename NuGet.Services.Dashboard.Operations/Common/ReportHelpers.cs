using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations.Common
{
    public static class ReportHelpers
    {
        public static string Load(CloudStorageAccount storageAccount, string name, string containerName = "dashboard")
        {            
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            string content;
            using (var memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            return content;
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
        public static List<Tuple<string,string>> AppendDatatoBlob(CloudStorageAccount account, string blobName, Tuple<string,string> tuple)
        {

            string json = Load( account, blobName);
            if (json == null)
            {
                return null;
            }

            List<Tuple<string, string>> dict = new List<Tuple<string, string>>();
            JArray array = JArray.Parse(json);            
            foreach (JObject item in array)
            {               
                dict.Add(new Tuple<string,string>(item["key"].ToString(), item["value"].ToString()));
            }
            dict.Add(tuple);
            if(dict.Count > 5)
            {
              dict =  dict.Skip(dict.Count - 5).ToList();
            }
            return dict;
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
