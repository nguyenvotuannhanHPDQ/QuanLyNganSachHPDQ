using Newtonsoft.Json;
using QuanLyNganSach.Helpers;
using QuanLyNganSach.Models.Auth;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace QuanLyNganSach.Controllers
{
    public class AccountController : BaseController
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();

        [AllowAnonymous]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string maNhanVien, string matKhau)
        {
            if (string.IsNullOrWhiteSpace(maNhanVien) || string.IsNullOrWhiteSpace(matKhau))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin";
                return View();
            }

            string passwordHash = SecurityHelper.Sha256Hash(matKhau);

            var user = db.Users
                .Where(x => x.MaNhanVien == maNhanVien
                         && x.Password == passwordHash
                         && (bool) x.IsActive)
                .Select(x => new LoggedInUser
                {
                    MaNhanVien = x.MaNhanVien,
                    UserName = x.HoTen,
                    RoleId = x.RoleId,
                })
                .FirstOrDefault();

            if (user == null)
            {
                ViewBag.Error = "Thông tin đăng nhập không đúng";
                return View();
            }

            string userData = JsonConvert.SerializeObject(user);

            var ticket = new FormsAuthenticationTicket(
                1,
                user.UserName,
                DateTime.Now,
                DateTime.Now.AddMinutes(60),
                false,
                userData
            );

            var cookie = new HttpCookie(
                FormsAuthentication.FormsCookieName,
                FormsAuthentication.Encrypt(ticket)
            )
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Expires = ticket.Expiration
            };

            Response.Cookies.Add(cookie);

            return RedirectToAction("Index", "Home");
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}