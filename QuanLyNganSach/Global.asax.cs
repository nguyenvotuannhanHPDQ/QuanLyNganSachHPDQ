using QuanLyNganSach.Constants;
using QuanLyNganSach.Models.Auth;
using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

namespace QuanLyNganSach
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
            if (!Request.IsAuthenticated)
            {
                return;
            }

            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null)
            {
                return;
            }

            var ticket = FormsAuthentication.Decrypt(authCookie.Value);

            if (ticket == null || string.IsNullOrEmpty(ticket.UserData))
            {
                return;
            }

            var loggedUser = Newtonsoft.Json.JsonConvert
                .DeserializeObject<LoggedInUser>(ticket.UserData);

            HttpContext.Current.Items[AuthConst.CurrentUser] = loggedUser;
        }
    }
}
