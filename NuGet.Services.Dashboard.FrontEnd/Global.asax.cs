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
        public static string currentEnvironmentName = "Prod0";

        public static string WorkServiceUserName = ConfigurationManager.AppSettings["WorkServiceUserNameProd0"];

        public static string WorkServiceAdminKey = ConfigurationManager.AppSettings["WorkServiceAdminKeyProd0"];

        public static string DBConnectionString = ConfigurationManager.AppSettings["DBConnectionStringProd0"];

        public static string ElmahAccountCredentials = ConfigurationManager.AppSettings["ElmahAccountCredentialsProd0"];

        private const string WorkServiceUserNamePrefix = "WorkServiceUserName";
        private const string WorkServiceAdminKeyPrefix = "WorkServiceAdminKey";
        private const string DBConnectionStringPrefix = "DBConnectionString";
        private const string ElmahAccountCredentialsPrefix = "ElmahAccountCredentials";

        public static void update()
        {
            WorkServiceUserName = ConfigurationManager.AppSettings[WorkServiceUserNamePrefix + currentEnvironmentName];
            WorkServiceAdminKey = ConfigurationManager.AppSettings[WorkServiceAdminKeyPrefix + currentEnvironmentName];
            DBConnectionString = ConfigurationManager.AppSettings[DBConnectionStringPrefix + currentEnvironmentName];
            ElmahAccountCredentials = ConfigurationManager.AppSettings[ElmahAccountCredentials + currentEnvironmentName];
        }
        protected void Application_Start()
        {
            // comment this line, since Nugetgallery MVC conflict with Dashboard MVC
         //   AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}