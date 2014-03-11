using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetDashboard.Models;
using System.Configuration;
using System.IO;
using System.Web.Script.Serialization;

namespace NuGetDashboard.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {        
            return View((object)MvcApplication.currentEnvironmentName);
        }

        public ActionResult UpdateEnvironment(string envName)
        {            
            if(MvcApplication.currentEnvironmentName == "Prod")
                 MvcApplication.currentEnvironmentName = "QA";
            else
                MvcApplication.currentEnvironmentName = "Prod";
           return RedirectToAction("Index");
        }

        public ActionResult Tiles()
        {
            return PartialView("~/Views/Home/Tiles.cshtml", MvcApplication.currentEnvironmentName);
        }
    
    }
}
