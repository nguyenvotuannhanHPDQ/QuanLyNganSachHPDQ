using Newtonsoft.Json;
using QuanLyNganSach.Helpers;
using QuanLyNganSach.Models.Auth;
using QuanLyNganSach.Models.ViewModels;
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
                    UserId = x.UserId,
                    MaNhanVien = x.MaNhanVien,
                    UserName = x.MaNhanVien,
                    HoTen = x.HoTen,
                    PhongBanId = (int) x.PhongBanId,
                    TenPhongBan = x.PhongBan.TenPhongBan,
                    MaPhongBan = x.PhongBan.MaPhongBan,
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

        // GET: Account/ChangePassword
        [Authorize]
        public ActionResult ChangePassword()
        {
            return View();
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Lấy thông tin user hiện tại
            var username = CurrentUser.UserName;
            var user = db.Users.FirstOrDefault(u => u.MaNhanVien == username);

            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin tài khoản.";
                return View(model);
            }

            // Kiểm tra mật khẩu hiện tại
            var currentPasswordHash = SecurityHelper.Sha256Hash(model.CurrentPassword);
            if (user.Password != currentPasswordHash)
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng");
                return View(model);
            }

            // Kiểm tra mật khẩu mới không được trùng mật khẩu cũ
            var newPasswordHash = SecurityHelper.Sha256Hash(model.NewPassword);
            if (user.Password == newPasswordHash)
            {
                ModelState.AddModelError("NewPassword", "Mật khẩu mới không được trùng với mật khẩu hiện tại");
                return View(model);
            }

            // Cập nhật mật khẩu mới
            user.Password = newPasswordHash;
            db.SaveChanges();

            TempData["Success"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";

            // Đăng xuất sau khi đổi mật khẩu
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Login", "Account");
        }
    }
}