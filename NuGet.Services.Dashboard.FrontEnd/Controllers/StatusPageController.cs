using System.Security.Claims;
using System.Web.Mvc;
using NuGetDashboard.Models;
using NuGetDashboard.Utilities;

namespace NuGetDashboard.Controllers
{
    [Authorize]
    public class StatusPageController : Controller
    {
        [HttpGet]
        [ActionName("Create")]
        public ActionResult Create_Get(bool posted = false)
        {
            ViewBag.Posted = posted;
            return View(new StatusPageMessageViewModel());
        }

        [HttpPost]
        [ActionName("Create")]
        public ActionResult Create_Post(StatusPageMessageViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (TableStorageService.WriteStatusPageMessage(model.Environment, model.When, model.Prefix + model.Message, 
                    ((ClaimsIdentity)User.Identity).FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress").Value))
                {
                    return RedirectToAction("Create", new { posted = true });
                }
            }
            return View(model);
        }
    }
}