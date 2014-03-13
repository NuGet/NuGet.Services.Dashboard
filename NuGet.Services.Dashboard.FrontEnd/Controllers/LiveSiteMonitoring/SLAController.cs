using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetDashboard.Utilities;
using DotNet.Highcharts.Helpers;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    /// <summary>
    /// Provides details about the server side SLA : Error rate ans requests per hour.
    public class SLAController : Controller
    {
        public ActionResult Index()
        {            
            return PartialView("~/Views/SLA/SLA_Index.cshtml" );
        }

        [HttpGet]
        public ActionResult Details()
        {
            return PartialView("~/Views/SLA/SLA_Details.cshtml");
        }

        [HttpGet]
        public ActionResult RequestsInOneHour()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("RequestsInOneHour"));
        }

        [HttpGet]
        public ActionResult RequestsInSixHour()
        {           
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("RequestsInSixHour"));
        }

        [HttpGet]
        public ActionResult RequestsInOneDay()
        {  
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("RequestsIn24Hour"));
        }

        [HttpGet]
        public ActionResult ErrorRate()
        {  
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("ErrorRate","ErrorsPerHour"));
        }

        [HttpGet]
        public ActionResult Throughput()
        {
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            List<string> xValues = new List<string>();
            List<Object> yValues = new List<Object>();
            BlobStorageService.GetJsonDataFromBlob("IISRequests" + string.Format("{0:MMdd}", DateTime.Now) + ".json", out xValues, out yValues);

            //get data for the last 6 hours only for front page.
            if(xValues.Count > 6)
            {
            xValues.RemoveRange(0,xValues.Count-6);
             yValues.RemoveRange(0,yValues.Count-6);
            }

            seriesSet.Add(new DotNet.Highcharts.Options.Series
            {
                Data = new Data(yValues.ToArray()),
                Name = "RequestsPerHour"
            });
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetAreaChart(seriesSet,xValues, "RequestsPerHour"));
        }

        [HttpGet]
        public JsonResult GetCurrentThroughputStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IISRequests" + string.Format("{0:MMdd}", DateTime.Now) + ".json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetCurrentErrorRateStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("ErrorRate.json");
            return Json(dict.Values.ElementAt(dict.Count-2), JsonRequestBehavior.AllowGet);
        }
    }
}
