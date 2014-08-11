using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetDashboard.Utilities;
using DotNet.Highcharts.Helpers;
using NuGet.Services.Dashboard.Common;
using System.Web.Script.Serialization;
using System.Configuration;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    /// <summary>
    /// Provides details about the server side SLA : Error rate ans requests per hour.
    public class V2GalleryFrontEndController : Controller
    {
        public ActionResult V2GalleryFrontEnd_Index()
        {
            return PartialView("~/Views/V2GalleryFrontEnd/V2GalleryFrontEnd_Index.cshtml");
        }

        [HttpGet]
        public ActionResult V2GalleryFrontEnd_Details()
        {
            return View("~/Views/V2GalleryFrontEnd/V2GalleryFrontEnd_Details.cshtml");
        }

        [HttpGet]
        public ActionResult ErrorsThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "ErrorRate" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "ErrorsPerHour", 24, 800));
        }

        [HttpGet]
        public ActionResult RequestsThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "IISRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "RequestsPerHour", 24, 800));
        }

        [HttpGet]
        public ActionResult RequestsToday()
        {
            List<Tuple<string, string, string, double>> scenarios = GetRequestsData(String.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()));
            return PartialView("~/Views/V2GalleryFrontEnd/V2GalleryFrontEnd_RequestDetails.cshtml", scenarios);
        }


        [HttpGet]
        public ActionResult LatencyToday()
        {
            List<Tuple<string, double, double, double>> scenarios = GetLatencyData(String.Format("{0:d}", DateTimeUtility.GetPacificTimeNow()));
            ViewBag.catalog = GetCatalogLag();
            ViewBag.resolver = GetResolverLag();
            return PartialView("~/Views/V2GalleryFrontEnd/V2GalleryFrontEnd_LatencyDetails.cshtml", scenarios);
        }

        [HttpGet]
        public ActionResult LatencyReport()
        {
            string today = DateTime.Today.ToString("d");
            string[] blobNames = new string[3];
            blobNames[0] = "UploadPackageTimeElapsed" + today;
            blobNames[1] = "SearchPackageTimeElapsed" + today;
            blobNames[2] = "DownloadPackageTimeElapsed" + today;
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "MillisecondsPerUpload", 3, 800));
        }

        [HttpGet]
        public ActionResult RequestPerHourTrendToday()
        {
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            List<string> value = new List<string>();
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IISRequestDetails" + String.Format("{0:MMdd}", DateTime.Now.AddDays(-1)) + ".json");
            List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
            Dictionary<string, List<object>> request = new Dictionary<string, List<object>>();
            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    value.Add(keyValuePair.Key.Substring(0, 2));
                    requestDetails = new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(keyValuePair.Value);

                    foreach (IISRequestDetails scenarios in requestDetails)
                    {
                        if (scenarios.ScenarioName.Equals("Over all requests")) continue;
                        if (request.ContainsKey(scenarios.ScenarioName))
                        {
                            request[scenarios.ScenarioName].Add(scenarios.RequestsPerHour);
                        }
                        else
                        {
                            List<object> Yvalue = new List<object>();
                            Yvalue.Add(scenarios.RequestsPerHour);
                            request.Add(scenarios.ScenarioName, Yvalue);
                        }
                    }
                }

                foreach (KeyValuePair<string, List<object>> each in request)
                {
                    seriesSet.Add(new DotNet.Highcharts.Options.Series
                    {
                        Data = new Data(each.Value.ToArray()),
                        Name = each.Key.Replace(" ", "_")
                    });
                }
            }
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, value, "TodayRequestPerHourTrend", 500);
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public ActionResult AverageTimeTakenInMsTrendToday()
        {
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            List<string> value = new List<string>();
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IISRequestDetails" + String.Format("{0:MMdd}", DateTime.Now.AddDays(-1)) + ".json");
            if (dict != null)
            {
                List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
                Dictionary<string, List<object>> request = new Dictionary<string, List<object>>();
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    value.Add(keyValuePair.Key.Substring(0, 2));
                    requestDetails = new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(keyValuePair.Value);

                    foreach (IISRequestDetails scenarios in requestDetails)
                    {
                        if (scenarios.ScenarioName.Equals("Over all requests")) continue;
                        if (request.ContainsKey(scenarios.ScenarioName))
                        {
                            request[scenarios.ScenarioName].Add(scenarios.AvgTimeTakenInMilliSeconds);
                        }
                        else
                        {
                            List<object> Yvalue = new List<object>();
                            Yvalue.Add(scenarios.AvgTimeTakenInMilliSeconds);
                            request.Add(scenarios.ScenarioName, Yvalue);
                        }
                    }
                }

                foreach (KeyValuePair<string, List<object>> each in request)
                {
                    seriesSet.Add(new DotNet.Highcharts.Options.Series
                    {
                        Data = new Data(each.Value.ToArray()),
                        Name = each.Key.Replace(" ", "_")
                    });
                }
            }
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, value, "TodayAvgTimeInMsTrend", 500);
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public ActionResult AverageRequestPerHourTrendThisWeek()
        {
            List<string> value = new List<string>();
            Dictionary<string, List<object>> request = new Dictionary<string, List<object>>();
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            DateTime start = DateTimeUtility.GetPacificTimeNow().AddDays(-8);
            for (int i = 0; i < 8; i++)
            {
                string date = string.Format("{0:MMdd}", start.AddDays(i));
                List<Tuple<string, string, string, double>> scenarios = GetRequestsData(date);
                value.Add(string.Format("{0:MM/dd}", start.AddDays(i)));
                for (int j = 1; j < scenarios.Count; j++)
                {
                    if (request.ContainsKey(scenarios[j].Item1))
                    {
                        request[scenarios[j].Item1].Add(scenarios[j].Item2);
                    }
                    else
                    {
                        List<object> Yvalue = new List<object>();
                        Yvalue.Add(scenarios[j].Item2);
                        request.Add(scenarios[j].Item1, Yvalue);
                    }
                }
            }

            foreach (KeyValuePair<string, List<object>> each in request)
            {
                seriesSet.Add(new DotNet.Highcharts.Options.Series
                {
                    Data = new Data(each.Value.ToArray()),
                    Name = each.Key.Replace(" ", "_")
                });
            }
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, value, "WeeklyAvgRequestPerHourTrend", 500);
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public ActionResult AverageTimeTakenInMsTrendThisWeek()
        {
            List<string> value = new List<string>();
            Dictionary<string, List<object>> time = new Dictionary<string, List<object>>();
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            DateTime start = DateTimeUtility.GetPacificTimeNow().AddDays(-8);
            for (int i = 0; i < 8; i++)
            {
                string date = string.Format("{0:MMdd}", start.AddDays(i));
                List<Tuple<string, string, string, double>> scenarios = GetRequestsData(date);
                value.Add(string.Format("{0:MM/dd}", start.AddDays(i)));
                for (int j = 1; j < scenarios.Count; j++)
                {
                    if (time.ContainsKey(scenarios[j].Item1))
                    {
                        time[scenarios[j].Item1].Add(scenarios[j].Item3.ToString());
                    }

                    else
                    {
                        List<object> Yvalue = new List<object>();
                        Yvalue.Add(scenarios[j].Item3.ToString());
                        time.Add(scenarios[j].Item1, Yvalue);
                    }
                }
            }

            foreach (KeyValuePair<string, List<object>> each in time)
            {
                seriesSet.Add(new DotNet.Highcharts.Options.Series
                {
                    Data = new Data(each.Value.ToArray()),
                    Name = each.Key.Replace(" ", "_")
                });
            }
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, value, "WeeklyAvgTimeInMsTrend", 500);
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        //HERE: CHANGE TO USE LATENCY REPORTS
        [HttpGet]
        public ActionResult AverageLatencyTrendThisWeek()
        {
            List<string> value = new List<string>();
            Dictionary<string, List<object>> time = new Dictionary<string, List<object>>();
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            DateTime start = DateTimeUtility.GetPacificTimeNow().AddDays(-7);
            for (int i = 0; i <= 7; i++)
            {
                string date = string.Format("{0:d}", start.AddDays(i));
                List<Tuple<string, double, double, double>> scenarios = GetLatencyData(date);
                value.Add(string.Format("{0:d}", start.AddDays(i)));
                for (int j = 0; j < scenarios.Count; j++)
                {
                    if (time.ContainsKey(scenarios[j].Item1))
                    {
                        time[scenarios[j].Item1].Add(scenarios[j].Item2.ToString());
                    }

                    else
                    {
                        List<object> Yvalue = new List<object>();
                        Yvalue.Add(scenarios[j].Item2.ToString());
                        time.Add(scenarios[j].Item1, Yvalue);
                    }
                }
            }

            foreach (KeyValuePair<string, List<object>> each in time)
            {
                seriesSet.Add(new DotNet.Highcharts.Options.Series
                {
                    Data = new Data(each.Value.ToArray()),
                    Name = each.Key.Replace(" ", "_")
                });
            }

            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, value, "WeeklyAvgLatencyTrend", 500);
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        private List<Tuple<string, double, double, double>> GetLatencyData(string date)
        {
            Dictionary<string, string> uploadDict = BlobStorageService.GetDictFromBlob("UploadPackageTimeElapsed" + date + ".json");
            Dictionary<string, string> searchDict = BlobStorageService.GetDictFromBlob("SearchPackageTimeElapsed" + date + ".json");
            Dictionary<string, string> downloadDict = BlobStorageService.GetDictFromBlob("DownloadPackageTimeElapsed" + date + ".json");
            List<Tuple<string, double, double, double>> result = new List<Tuple<string, double, double, double>>();
            if (uploadDict != null)
            {
                List<double> latency = new List<double>();
                foreach (KeyValuePair<string, string> keyValuePair in uploadDict)
                {
                    latency.Add(Convert.ToDouble(keyValuePair.Value));
                }

                double average = latency.Average();
                double highest = latency.Max();
                double lowest = latency.Min();
                result.Add(new Tuple<string, double, double, double>("Upload", average, highest, lowest));
            }

            if (searchDict != null)
            {
                List<double> latency = new List<double>();
                foreach (KeyValuePair<string, string> keyValuePair in searchDict)
                {
                    latency.Add(Convert.ToDouble(keyValuePair.Value));
                }

                double average = latency.Average();
                double highest = latency.Max();
                double lowest = latency.Min();
                result.Add(new Tuple<string, double, double, double>("Search", average, highest, lowest));
            }

            if (downloadDict != null)
            {
                List<double> latency = new List<double>();
                foreach (KeyValuePair<string, string> keyValuePair in downloadDict)
                {
                    latency.Add(Convert.ToDouble(keyValuePair.Value));
                }

                double average = latency.Average();
                double highest = latency.Max();
                double lowest = latency.Min();
                result.Add(new Tuple<string, double, double, double>("Download", average, highest, lowest));
            }


            return result;
        }

        [HttpGet]
        public string GetCatalogLag()
        {
            string blobName;
            string date = String.Format("{0:d}", DateTimeUtility.GetPacificTimeNow());
            blobName = "CatalogLag" + date + ".json";
            Dictionary<string, string> CatalogDict = BlobStorageService.GetDictFromBlob(blobName);
            List<TimeSpan> timeStamps = new List<TimeSpan>();
            if (CatalogDict != null && CatalogDict.Count > 0)
            {
                foreach (KeyValuePair<string, string> entry in CatalogDict)
                {
                    DateTime time = DateTime.Parse(string.Format("{0:HH:mm}", entry.Key));
                    timeStamps.Add(time.TimeOfDay);
                }

                string latest = timeStamps.Max().Hours + ":" + timeStamps.Max().Minutes;
                string value = CatalogDict[latest.ToString()];
                return "Lag: " + value; ; 
            }

            else return "Lag: N/A";
        }

        [HttpGet]
        public string GetResolverLag()
        {
            string blobName;
            string date = String.Format("{0:d}", DateTimeUtility.GetPacificTimeNow());
            blobName = "ResolverLag" + date + ".json";
            Dictionary<string, string> ResolverDict = BlobStorageService.GetDictFromBlob(blobName);
            List<TimeSpan> timeStamps = new List<TimeSpan>();
            if (ResolverDict != null && ResolverDict.Count > 0)
            {
                foreach (KeyValuePair<string, string> entry in ResolverDict)
                {
                    DateTime time = DateTime.Parse(string.Format("{0:HH:mm}", entry.Key));
                    timeStamps.Add(time.TimeOfDay);
                }

                string latest = timeStamps.Max().Hours + ":" + timeStamps.Max().Minutes;
                string value = ResolverDict[latest.ToString()];
                return "Lag: " + value; 
            }

            else return "Lag: N/A";
        }

        [HttpGet]
        public JsonResult GetHourlyPackagetatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Uploads" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            if (dict != null && dict.Count > 0)
            {
                //find the sum of values of each hour from today's report.
                int sum = 0;
                foreach (KeyValuePair<string, string> pair in dict)
                {
                    int count = Convert.ToInt32(pair.Value);
                    sum = sum + count;
                }
                return Json(sum.ToString(), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json("N/A");
            }
        }

        private List<Tuple<string, string, string, double>> GetRequestsData(string date)
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IISRequestDetails" + date + ".json");
            List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
            List<Tuple<string, string, string, double>> scenarios = new List<Tuple<string, string, string, double>>();
            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    requestDetails.AddRange(new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(keyValuePair.Value));
                }

                var requestGroups = requestDetails.GroupBy(item => item.ScenarioName);

                foreach (IGrouping<string, IISRequestDetails> group in requestGroups)
                {
                    scenarios.Add(new Tuple<string, string, string, double>(group.Key, Convert.ToInt32(group.Average(item => item.RequestsPerHour)).ToString(), Convert.ToInt32(group.Max(item => item.RequestsPerHour)).ToString(), Convert.ToInt32(group.Average(item => item.AvgTimeTakenInMilliSeconds))));
                }
            }

            return scenarios;
        }

        [HttpGet]
        public ActionResult ErrorRate()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("ErrorRate" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "ErrorsPerHour"));
        }

        [HttpGet]
        public ActionResult Throughput()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("IISRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "RequestsPerHour"));
        }

        [HttpGet]
        public JsonResult GetCurrentThroughputStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IISRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + ".json");
            if (dict != null && dict.Count > 0)
                return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
            else
                return Json("N/A");
        }

        [HttpGet]
        public JsonResult GetCurrentErrorRateStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("ErrorRate" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + ".json");
            if (dict != null && dict.Count > 0)
                return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
            else
                return Json("N/A");
        }

        /// <summary>
        /// Returns the detailed report on Elmah for the past N hours.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public ActionResult ElmahErrorSummary(string hour)
        {
            var content = BlobStorageService.Load("ElmahErrorsDetailed" + hour + "hours.json");
            List<ElmahError> listOfEvents = new List<ElmahError>();
            if (content != null)
            {
                listOfEvents = new JavaScriptSerializer().Deserialize<List<ElmahError>>(content);
            }
            return PartialView("~/Views/V2GalleryFrontEnd/ElmahErrorSummary.cshtml", listOfEvents);
        }


        public ActionResult RefreshElmah()
        {
            List<ElmahError> listOfEvents = new List<ElmahError>();
            RefreshElmahError RefreshExecute = new RefreshElmahError(ConfigurationManager.AppSettings["StorageConnection"],
                                                                     MvcApplication.StorageContainer,
                                                                     1,
                                                                     MvcApplication.ElmahAccountCredentials);


            listOfEvents = RefreshExecute.ExecuteRefresh();

            return PartialView("~/Views/V2GalleryFrontEnd/ElmahErrorSummary.cshtml", listOfEvents);
        }
    }
}
