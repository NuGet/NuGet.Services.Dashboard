using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace NuGetDashboard
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static string DBConnectionString = ConfigurationManager.AppSettings["V2GalleryDBConnectionStringProd0"];

        public static string ElmahAccountCredentials = ConfigurationManager.AppSettings["ElmahAccountProd0"];

        public static string StorageContainer = ConfigurationManager.AppSettings["StorageContainerProd0"];

        public const string DBConnectionStringPrefix = "V2GalleryDBConnectionString";
        public const string ElmahAccountCredentialsPrefix = "ElmahAccount";
        public const string StorageContainerPrefix = "StorageContainer";

        protected void Application_Start()
        {
            // comment this line, since Nugetgallery MVC conflict with Dashboard MVC
         //   AreaRegistration.RegisterAllAreas();
            
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Session_Start()
        {
            Session["currentEnvironmentName"] = "Prod0";
        }

        


    }
}