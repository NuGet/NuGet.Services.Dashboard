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
            for (int i = 0; i < 8;i++)
                   blobNames[i] = "IISRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
                return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "RequestsPerHour", 24, 800));
        }

        [HttpGet]
        public ActionResult ErrorRate()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("ErrorRate" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "ErrorsPerHour"));
        }

        [HttpGet]
        public ActionResult Throughput()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("IISRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) , "RequestsPerHour"));
        }

        [HttpGet]
        public JsonResult GetCurrentThroughputStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IISRequests" + string.Format("{0:MMdd}",DateTimeUtility.GetPacificTimeNow()) + ".json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetCurrentErrorRateStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("ErrorRate" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + ".json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }
    }
}
