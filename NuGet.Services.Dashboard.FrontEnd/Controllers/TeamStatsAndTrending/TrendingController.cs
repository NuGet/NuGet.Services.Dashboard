using DotNet.Highcharts;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Trending
{
    public class TrendingController : Controller
    {        
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Details()
        {
            return PartialView("~/Views/Trending/Trending_Details.cshtml");
        }

        [HttpGet]
        public ActionResult PackagesThiSWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "Uploads" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime().AddDays(-i)) + "HourlyReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "UploadsPerHour", 24, 800));
        }

        [HttpGet]
        public ActionResult UsersThiSWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "Users" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime().AddDays(-i)) + "HourlyReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "UsersPerHour", 24, 800));
        }

        [HttpGet]
        public JsonResult GetHourlyPackagetatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Uploads" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime()) + "HourlyReport.json");
            return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetHourlyUsertatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Users" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime()) + "HourlyReport.json");
            return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
        }

    }
}
