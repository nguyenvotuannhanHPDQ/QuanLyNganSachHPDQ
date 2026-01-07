using PagedList;
using QuanLyNganSach.Constants;
using QuanLyNganSach.Controllers;
using QuanLyNganSach.Filters;
using QuanLyNganSach.Helpers;
using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace QuanLyNganSach.Areas.Admin.Controllers
{
    [RoleAuthorize(RoleId = RoleConst.Admin)]
    public class UserController : BaseController
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();
        private const int PageSize = 10;

        // GET: Admin/User/Index
        public ActionResult Index(int? page, string search, string status)
        {
            int pageNumber = page ?? 1;

            var query =
                from u in db.Users
                join r in db.Roles on u.RoleId equals r.RoleId
                join p in db.PhongBans on u.PhongBanId equals p.PhongBanId
                select new UserListViewModel
                {
                    UserId = u.UserId,
                    MaNhanVien = u.MaNhanVien,
                    HoTen = u.HoTen,
                    RoleName = r.RoleName,
                    TenPhongBan = p.TenPhongBan,
                    IsActive = (bool) u.IsActive,
                    CreatedDate = (DateTime) u.CreatedDate
                };

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u =>
                u.MaNhanVien.ToLower().Contains(search) ||
                u.HoTen.ToLower().Contains(search) ||
                u.RoleName.ToLower().Contains(search));
            }

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                bool isActive = status == "active";
                query = query.Where(u => u.IsActive == isActive);
            }

            var users = query
                .OrderByDescending(u => u.CreatedDate)
                .ToPagedList(pageNumber, PageSize);

            // Pass parameters to view for maintaining state
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status ?? "all";

            return View(users);
        }

        // GET: Admin/User/Create
        public ActionResult Create()
        {
            var model = new CreateUserViewModel
            {
                Roles = GetRolesList(),
                PhongBans = GetPhongBanList()
            };
            return View(model);
        }

        // POST: Admin/User/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateUserViewModel model)
        {
            // Validate ModelState
            if (!ModelState.IsValid)
            {
                model.Roles = GetRolesList();
                model.PhongBans = GetPhongBanList();
                return View(model);
            }

            // Kiểm tra mã nhân viên trùng
            if (db.Users.Any(x => x.MaNhanVien == model.MaNhanVien))
            {
                ModelState.AddModelError("MaNhanVien", "Mã nhân viên đã tồn tại");
                model.Roles = GetRolesList();
                model.PhongBans = GetPhongBanList();
                return View(model);
            }

            // Tạo user mới
            var user = new User
            {
                MaNhanVien = model.MaNhanVien,
                HoTen = model.HoTen,
                RoleId = model.RoleId,
                PhongBanId = model.PhongBanId,
                Password = SecurityHelper.Sha256Hash(PasswordConst.DEFAULT_PASSWORD),
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            db.Users.Add(user);
            db.SaveChanges();

            TempData["Success"] = "Tạo tài khoản thành công!";
            return RedirectToAction("Create");
        }

        // GET: Admin/User/Edit/5
        public ActionResult Edit(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản cần chỉnh sửa";
                return RedirectToAction("Index");
            }

            var model = new EditUserViewModel
            {
                UserId = user.UserId,
                MaNhanVien = user.MaNhanVien,
                HoTen = user.HoTen,
                PhongBanId = (int) user.PhongBanId,
                RoleId = user.RoleId,
                IsActive = (bool) user.IsActive
            };

            model.Roles = GetRolesList();
            model.PhongBans = GetPhongBanList();

            return View(model);
        }

        // POST: Admin/User/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Roles = GetRolesList();
                model.PhongBans = GetPhongBanList();
                return View(model);
            }

            // Kiểm tra mã nhân viên trùng
            if (db.Users.Any(x => x.MaNhanVien == model.MaNhanVien && x.UserId != model.UserId))
            {
                ModelState.AddModelError("MaNhanVien", "Mã nhân viên đã tồn tại");
                model.Roles = GetRolesList();
                return View(model);
            }

            var user = db.Users.Find(model.UserId);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản cần cập nhật";
                return RedirectToAction("Index");
            }

            // Cập nhật thông tin
            user.MaNhanVien = model.MaNhanVien;
            user.HoTen = model.HoTen;
            user.RoleId = model.RoleId;
            user.PhongBanId = model.PhongBanId;
            user.IsActive = model.IsActive;

            db.SaveChanges();

            TempData["Success"] = $"Cập nhật thông tin tài khoản <strong>{model.HoTen}</strong> thành công!";
            return RedirectToAction("Index");
        }

        // POST: Admin/User/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản" });
            }

            // Xóa luận lý - chỉ đánh dấu IsActive = false
            user.IsActive = false;
            db.SaveChanges();

            return Json(new { success = true, message = $"Đã vô hiệu hóa tài khoản <strong>{user.HoTen}</strong>." });
        }

        // POST: Admin/User/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id)
        {
            try
            {
                var user = db.Users.Find(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản." });
                }

                // Không cho phép reset mật khẩu chính mình
                if (user.MaNhanVien == CurrentUser.MaNhanVien)
                {
                    return Json(new { success = false, message = "Không thể reset mật khẩu của chính bạn." });
                }

                // Reset về mật khẩu mặc định
                user.Password = SecurityHelper.Sha256Hash(PasswordConst.DEFAULT_PASSWORD);

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã reset mật khẩu cho tài khoản <strong>{user.HoTen}</strong>.<br/>"
                });
            }
            catch (Exception ex)
            {
                // Log error here
                return Json(new { success = false, message = "Đã xảy ra lỗi khi reset mật khẩu. Vui lòng thử lại." });
            }
        }

        private IEnumerable<SelectListItem> GetRolesList()
        {
            return db.Roles
                .OrderBy(r => r.RoleName)
                .Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = r.RoleName
                })
                .ToList();
        }

        private IEnumerable<SelectListItem> GetPhongBanList()
        {
            return db.PhongBans
                .Where(x => x.IsActive)
                .OrderBy(x => x.TenPhongBan)
                .Select(x => new SelectListItem
                {
                    Value = x.PhongBanId.ToString(),
                    Text = x.TenPhongBan
                })
                .ToList();
        }
    }
}