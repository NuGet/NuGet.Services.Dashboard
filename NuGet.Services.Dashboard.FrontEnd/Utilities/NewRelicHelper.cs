using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Xml.Serialization;

namespace NuGetDashboard.Utilities
{
    /// <summary>
    /// Helper methods to Query the New relic API.
    /// </summary>
    public class NewRelicHelper
    {       
        public static List<threshold_value> GetLatestMetrics()
        {
            WebRequest request = WebRequest.Create("https://api.newrelic.com/api/v1/accounts/" + ConfigurationManager.AppSettings["NewRelicAccountID"] + "/applications/2586045/threshold_values.xml");
            request.Headers.Add(ConfigurationManager.AppSettings["NewRelicAppKey"]);
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            string text;
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                text = reader.ReadToEnd();
            }
            //The data returned from rest API is not XML deserializable. Hence do some text replacements to fix it.
            text = text.Replace("threshold_value=", "threshold=");
            text = text.Replace(@"threshold-values","ThresholdValues") ;
            text = text.Replace(@"type="+ @""""+ "array" +@"""" , "");

            string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            StreamWriter sw = new StreamWriter(tempFilePath);
            sw.Write(text);
            sw.Flush();
            sw.Close();

            XmlSerializer xs = new XmlSerializer(typeof(List<threshold_value>), new XmlRootAttribute("ThresholdValues"));
            Stream filereader = new FileStream(tempFilePath, FileMode.Open);
            List<threshold_value> metricList = (List<threshold_value>)xs.Deserialize(filereader);
            return metricList;
        }

    }

    #region XMLSerializationClasses

    [XmlTypeAttribute(AnonymousType = true)]
    public class ThresholdData
    {
        [XmlArray(ElementName = "ThresholdValues", Namespace = "http://www.w3.org/2001/XMLSchema-instance", IsNullable = false)]
        [XmlArrayItem(ElementName = "threshold_value", Type = typeof(threshold_value))]
        public List<threshold_value> Values { get; set; }

        public ThresholdData()
        {
            Values = new List<threshold_value>();
        }
    }

    public class threshold_value
    {
        [System.Xml.Serialization.XmlAttribute(AttributeName = "name")]
        public string name { get; set; }

        [System.Xml.Serialization.XmlAttribute(AttributeName = "metric_value")]
        public string metric_value { get; set; }

        [System.Xml.Serialization.XmlAttribute(AttributeName = "threshold")]
        public string threshold { get; set; }

        [System.Xml.Serialization.XmlAttribute(AttributeName = "begin_time")]
        public string begin_time { get; set; }

        [System.Xml.Serialization.XmlAttribute(AttributeName = "end_time")]
        public string end_time { get; set; }

        [System.Xml.Serialization.XmlAttribute(AttributeName = "formatted_metric_value")]
        public string formatted_metric_value { get; set; }


    }
    #endregion XMLSerializationClasses
}