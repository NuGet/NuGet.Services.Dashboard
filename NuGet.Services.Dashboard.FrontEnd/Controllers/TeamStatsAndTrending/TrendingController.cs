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

        public ActionResult Monthly()
        {
            return PartialView("~/Views/Trending/Trending_Monthly.cshtml");
        }

        public ActionResult Daily()
        {
            return PartialView("~/Views/Trending/Trending_Details.cshtml");
        }

        //Returns the overall trend chart for packages 
        public ActionResult PackagesChart()
        {
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChartFromBlobName("UploadsoctoberMonthlyReport", "Packages");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        //Returns the overall trend chart for users
        public ActionResult UsersChart()
        {
            DotNet.Highcharts.Highcharts chart =  ChartingUtilities.GetLineChartFromBlobName("UsersoctoberMonthlyReport", "Users");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
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
