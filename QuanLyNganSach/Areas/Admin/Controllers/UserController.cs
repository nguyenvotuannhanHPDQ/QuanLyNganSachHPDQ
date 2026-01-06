using PagedList;
using QuanLyNganSach.Constants;
using QuanLyNganSach.Controllers;
using QuanLyNganSach.Filters;
using QuanLyNganSach.Helpers;
using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
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

            var query = db.Users
                .Join(db.Roles,
                user => user.RoleId,
                role => role.RoleId,
                (user, role) => new UserListViewModel
                {
                    UserId = user.UserId,
                    MaNhanVien = user.MaNhanVien,
                    HoTen = user.HoTen,
                    RoleName = role.RoleName,
                    IsActive = (bool)user.IsActive,
                    CreatedDate = (DateTime)user.CreatedDate
                });

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
                Roles = GetRolesList()
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
                return View(model);
            }

            // Kiểm tra mã nhân viên trùng
            if (db.Users.Any(x => x.MaNhanVien == model.MaNhanVien))
            {
                ModelState.AddModelError("MaNhanVien", "Mã nhân viên đã tồn tại");
                model.Roles = GetRolesList();
                return View(model);
            }

            // Tạo user mới
            var user = new User
            {
                MaNhanVien = model.MaNhanVien,
                HoTen = model.HoTen,
                RoleId = model.RoleId,
                Password = SecurityHelper.Sha256Hash("123456"),
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
                RoleId = user.RoleId,
                IsActive = (bool) user.IsActive
            };

            model.Roles = GetRolesList();
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
    }
}