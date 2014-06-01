using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class SearchServiceController : Controller
    {
        //
        // GET: /SearchService/

        public ActionResult Index()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_SearchServiceCpuMem.cshtml"); ;
        }

        [HttpGet]
        public JsonResult GetSearchServiceStatus()
        {
            Dictionary<string, string> dict_cpu = BlobStorageService.GetDictFromBlob("SearchCpuUsage" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json");
            Dictionary<string, string> dict_mem = BlobStorageService.GetDictFromBlob("SearchMemUsage" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json");
            StringBuilder sb = new StringBuilder();
            sb.Append("<h1>" + "CPU Seconds" + "</h1> </br>");
            if (dict_cpu != null && dict_cpu.Count > 0)
            {
                sb.Append(dict_cpu.Values.Last() + "</br>");
            }
            else
                sb.Append("N/A");

            sb.Append("<h1>" + "virual memory size" + "</h1> </br>");
            if (dict_mem != null && dict_mem.Count > 0)
            {
                sb.Append(dict_mem.Values.Last() + "</br>");
            }
            else
                sb.Append("N/A");

            return Json(sb.ToString(), JsonRequestBehavior.AllowGet);
           
        }

    }
}
