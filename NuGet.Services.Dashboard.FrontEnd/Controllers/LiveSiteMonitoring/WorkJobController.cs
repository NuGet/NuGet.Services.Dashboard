using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using NuGet.Services.Dashboard.Common;
using NuGetDashboard.Utilities;
using System.Text;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class WorkJobController : Controller
    {

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult WorkJobDetail()
        {
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            var content = BlobStorageService.Load("WorkJobDetail.json");
            if (content != null)
            {
                jobDetail = new JavaScriptSerializer().Deserialize<List<WorkInstanceDetail>>(content);
            }
            ViewBag.work = jobDetail;
            ViewBag.admin = MvcApplication.WorkServiceUserName;
            ViewBag.key = MvcApplication.WorkServiceAdminKey;
            return PartialView("~/Views/WorkJobs/WorkJobs_detail.cshtml");
        }
    }
}
