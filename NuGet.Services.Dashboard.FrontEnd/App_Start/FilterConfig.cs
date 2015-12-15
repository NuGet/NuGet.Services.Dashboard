using NuGetDashboard.Filters;
using System.Web.Mvc;

namespace NuGetDashboard
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new EnsureTeamClaimsAttribute());
        }
    }
}