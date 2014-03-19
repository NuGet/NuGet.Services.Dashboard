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
        
        [HttpGet]
        public ActionResult Now()
        {
            ViewBag.ControllerName = "ResourceMonitoring";
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Now.cshtml");
        }     

        [HttpGet]
        public ActionResult Details()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Details.cshtml");
        }

        [HttpGet]
        public ActionResult Instance0CPU()
        {
            return GetChart("Instance0CPU");
        }

        [HttpGet]
        public ActionResult Instance1CPU()
        {
            return GetChart("Instance1CPU");
        }

        [HttpGet]
        public ActionResult Instance2CPU()
        {
            return GetChart("Instance2CPU");
        }

        [HttpGet]
        public ActionResult Instance0Memory()
        {
           return GetChart("Instance0Memory");

        }
        [HttpGet]
        public ActionResult Instance1Memory()
        {
           return GetChart("Instance1Memory");
        }

        [HttpGet]
        public ActionResult Instance2Memory()
        {
            return GetChart("Instance2Memory");
        }

        [HttpGet]
        public ActionResult DBCPUTime()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBCPUTime" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime()), "DBCPUTimeInSeconds"));
        }

        [HttpGet]
        public ActionResult DBRequests()
        {                   
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime()), "DBRequests",12));
        }

        [HttpGet]
        public ActionResult DBConnections()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBConnections" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime()), "DBConnections", 12));           
        }

        [HttpGet]
        public JsonResult GetCPUStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Instance0CPU.json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult DBCPUTimeThisWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "DBCPUTime" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBCPUTimeInSeconds", 50, 400));
        }
        [HttpGet]
        public ActionResult DBRequestsThisWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "DBRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBRequests", 50, 600));
        }
        [HttpGet]
        public ActionResult DBConnectionsThisWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "DBConnections" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBConnections", 50, 600));
        }


        [HttpGet]
        public JsonResult GetMemoryStatus()
        {
            //return Json(BlobStorageService.GetValueFromBlob("MemoryStatus.json", "Memory"), JsonRequestBehavior.AllowGet);
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Instance0Memory.json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }

        private ActionResult GetChart(string blobName)
        {   
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobName,blobName));
        }

    }
}
