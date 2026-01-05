using QuanLyNganSach.Constants;
using QuanLyNganSach.Filters;
using QuanLyNganSach.Helpers;
using QuanLyNganSach.Models.ViewModels;
using System;
using System.Linq;
using System.Web.Mvc;

namespace QuanLyNganSach.Controllers.Admin
{
    [RoleAuthorize(RoleId = RoleConst.Admin)]
    public class UserController : Controller
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();

        public ActionResult Create()
        {
            var model = new CreateUserViewModel
            {
                Roles = db.Roles.Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = r.RoleName
                })
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Roles = db.Roles.Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = r.RoleName
                });
                return View(model);
            }

            bool exists = db.Users.Any(x => x.MaNhanVien == model.MaNhanVien);
            if (exists)
            {
                ModelState.AddModelError("", "Mã nhân viên đã tồn tại");
                return View(model);
            }

            var user = new User
            {
                MaNhanVien = model.MaNhanVien,
                HoTen = model.HoTen,
                RoleId = model.RoleId,
                Password = SecurityHelper.Sha256Hash("123456"), // default
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            db.Users.Add(user);
            db.SaveChanges();

            TempData["Success"] = "Tạo tài khoản thành công";
            return RedirectToAction("Create");
        }
    }
}