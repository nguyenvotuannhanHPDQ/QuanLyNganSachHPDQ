using Newtonsoft.Json;
using QuanLyNganSach.Constants;
using QuanLyNganSach.Models.Auth;
using System.Web.Mvc;
using System.Web.Security;

namespace QuanLyNganSach.Filters
{
    public class CurrentUserFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var context = filterContext.HttpContext;

            if (!context.Request.IsAuthenticated)
                return;

            var identity = context.User.Identity as FormsIdentity;
            if (identity == null)
                return;

            var ticket = identity.Ticket;
            if (string.IsNullOrEmpty(ticket.UserData))
                return;

            try
            {
                var user = JsonConvert.DeserializeObject<LoggedInUser>(ticket.UserData);
                context.Items[AuthConst.CurrentUser] = user;
            }
            catch
            {
                FormsAuthentication.SignOut();
            }
        }

        public void OnActionExecuted(ActionExecutedContext filterContext)
        {
        }
    }
}