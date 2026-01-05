using QuanLyNganSach.Constants;
using QuanLyNganSach.Models.Auth;
using System.Web.Mvc;

namespace QuanLyNganSach.Controllers
{
    public abstract class BaseController : Controller
    {
        protected LoggedInUser CurrentUser
        {
            get
            {
                return HttpContext.Items[AuthConst.CurrentUser] as LoggedInUser;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.CurrentUser = CurrentUser; // use for view/layout
            base.OnActionExecuting(filterContext);
        }
    }
}