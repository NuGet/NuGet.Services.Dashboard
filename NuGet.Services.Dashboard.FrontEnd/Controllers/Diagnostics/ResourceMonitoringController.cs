using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Provides details about the resource utilization on the server : CPU, memory and DB.
    /// </summary>
    public class ResourceMonitoringController : Controller
    {
        public ActionResult Index()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Index.cshtml");
        }

        //[HttpGet]
        //public ActionResult DBCPUTime()
        //{
        //    return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBCPUTime" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "DBCPUTimeInSeconds"));
        //}

        [HttpGet]
        public ActionResult DBRequests()
        {                   
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "DBRequests",12));
        }

        [HttpGet]
        public ActionResult DBConnections()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBConnections" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "DBConnections", 12));           
        }      

        //[HttpGet]
        //public ActionResult DBCPUTimeThisWeek()
        //{
        //    string[] blobNames = new string[8];
        //    for (int i = 0; i < 8; i++)
        //        blobNames[i] = "DBCPUTime" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
        //    return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBCPUTimeInSeconds", 50, 400));
        //}
        [HttpGet]
        public ActionResult DBRequestsThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "DBRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBRequests", 50, 600));
        }
        [HttpGet]
        public ActionResult DBConnectionsThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "DBConnections" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBConnections", 50, 600));
        }

        [HttpGet]
        public JsonResult GetHourlyInstanceCount()
        {
            //TBD: Need to take the service name from the config.
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("nuget-prod-0-v2galleryInstanceCount" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public JsonResult GetCurrentIndexingStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IndexingDiffCount" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
        }

        private ActionResult GetChart(string blobName)
        {   
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobName,blobName));
        }

    }
}
