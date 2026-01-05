using QuanLyNganSach.Models.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QuanLyNganSach.Filters
{
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        public int RoleId { get; set; }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var user = httpContext.Items["LoggedUser"] as LoggedInUser;
            return user != null && user.RoleId == RoleId;
        }
    }
}