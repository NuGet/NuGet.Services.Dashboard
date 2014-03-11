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
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("Throughput","RequestsPerHour"));
        }

        [HttpGet]
        public JsonResult GetCurrentThroughputStatus()
        {
            return Json(BlobStorageService.GetValueFromBlob("CurrentRequestsStatus.json", "Requests"), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetCurrentErrorRateStatus()
        {
            return Json(BlobStorageService.GetValueFromBlob("CurrentErrorRateStatus.json", "ErrorRate"), JsonRequestBehavior.AllowGet);
        }
    }
}
