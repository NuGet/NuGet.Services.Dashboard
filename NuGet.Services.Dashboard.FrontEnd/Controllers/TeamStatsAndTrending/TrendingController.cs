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
                blobNames[i] = "Uploads" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i)) + "HourlyReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "UploadsPerHour", 24, 800));
        }

        [HttpGet]
        public ActionResult UsersThiSWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "Users" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i)) + "HourlyReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "UsersPerHour", 24, 800));
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

        [HttpGet]
        public JsonResult GetHourlyUsertatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Users" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
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

    }
}
