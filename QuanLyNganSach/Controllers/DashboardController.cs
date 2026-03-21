using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QuanLyNganSach.Controllers
{
    public class DashboardController : BaseController
    {
        // GET: Dashboard
        private readonly QuanLyNganSachEntities db
    = new QuanLyNganSachEntities();

        // ── GET: Dashboard/Index ─────────────────────────────────
        public ActionResult Index()
        {
            // Chỉ Admin/Manager mới được xem
            if (CurrentUser == null
             || (CurrentUser.RoleId != Constants.RoleConst.Admin
              && CurrentUser.RoleId != Constants.RoleConst.Manager))
            {
                TempData["Error"] = "Bạn không có quyền truy cập trang này.";
                return RedirectToAction("Index", "Home");
            }

            // Load dropdowns cho bộ lọc
            ViewBag.DsProjectArea = db.ProjectAreas
                .OrderBy(p => p.AreaName)
                .Select(p => new SelectListItem
                {
                    Value = p.ProjectAreaId.ToString(),
                    Text = p.AreaName
                }).ToList();

            ViewBag.DsPhongBan = db.PhongBans
                .Where(p => p.IsActive == true)
                .OrderBy(p => p.TenPhongBan)
                .Select(p => new SelectListItem
                {
                    Value = p.PhongBanId.ToString(),
                    Text = p.TenPhongBan
                }).ToList();

            ViewBag.DsPriorityLevel = db.BudgetPriorityLevels
                .OrderBy(p => p.PriorityLevelName)
                .Select(p => new SelectListItem
                {
                    Value = p.PriorityLevelId.ToString(),
                    Text = p.PriorityLevelName
                }).ToList();

            ViewBag.DsCategoryType = db.BudgetCategoryTypes
                .OrderBy(c => c.CategoryTypeName)
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryTypeId.ToString(),
                    Text = c.CategoryTypeName
                }).ToList();

            ViewBag.DsNam = new[]
            {
                new SelectListItem { Value = "2026", Text = "2026" },
                new SelectListItem { Value = "2027", Text = "2027" }
            };

            return View();
        }

        // ── GET: Dashboard/GetDashboardData (Ajax) ───────────────
        [HttpGet]
        public ActionResult GetDashboardData(DashboardFilterViewModel filter)
        {
            try
            {
                if (CurrentUser == null
                 || (CurrentUser.RoleId != Constants.RoleConst.Admin
                  && CurrentUser.RoleId != Constants.RoleConst.Manager))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không có quyền truy cập."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                // ── Query base ───────────────────────────────────
                //var query = db.BudgetRegistrations
                //    .Include(x => x.BudgetApprovals)
                //    .AsQueryable()
                var query = db.BudgetRegistrations
                .Include("BudgetApprovals")
                .AsQueryable();

                // ── Apply filters (AND) ──────────────────────────
                if (filter.ProjectAreaId.HasValue)
                    query = query.Where(x =>
                        x.ProjectAreaId == filter.ProjectAreaId.Value);

                if (filter.PhongBanId.HasValue)
                    query = query.Where(x =>
                        x.PhongBanId == filter.PhongBanId.Value);

                //if (filter.Nam.HasValue)
                //    query = query.Where(x =>
                //        x.CreatedDate.HasValue &&
                //        x.CreatedDate.Value.Year == filter.Nam.Value);

                if (filter.PriorityLevelId.HasValue)
                    query = query.Where(x =>
                        x.PriorityLevelId == filter.PriorityLevelId.Value);

                if (filter.CategoryTypeId.HasValue)
                    query = query.Where(x =>
                        x.CategoryTypeId == filter.CategoryTypeId.Value);

                // ── Materialize để tính toán ─────────────────────
                var data = query.Select(x => new
                {
                    x.BudgetRegistrationId,
                    x.DuToan,
                    x.SoToTrinh,
                    x.WorkflowType,
                    x.CreatedDate,
                    Approvals = x.BudgetApprovals.Select(a => new
                    {
                        a.IsSupplementary,
                        a.TrangThaiPheDuyet,
                        a.NganSachBoSung
                    })
                }).ToList();

                // ── Tính TrangThaiHienThi cho từng hồ sơ ────────
                // (Tái sử dụng logic từ BudgetRegistrationListViewModel)
                var dataWithStatus = data.Select(x =>
                {
                    var approvalGoc = x.Approvals
                        .FirstOrDefault(a => !a.IsSupplementary);
                    var trangThaiGoc = approvalGoc?.TrangThaiPheDuyet ?? 0;
                    var coBoSungChuaDuyet = x.Approvals
                        .Any(a => a.IsSupplementary
                               && a.TrangThaiPheDuyet != 2);
                    var coBoSungDaDuyet = x.Approvals
                        .Any(a => a.IsSupplementary
                               && a.TrangThaiPheDuyet == 2);

                    int trangThai;
                    if (string.IsNullOrEmpty(x.SoToTrinh))
                        trangThai = 0;
                    else if (x.WorkflowType == null)
                        trangThai = 1;
                    else if (x.WorkflowType == 2)
                        trangThai = 5;
                    else if (x.WorkflowType == 3)
                        trangThai = 6;
                    else if (trangThaiGoc == 2 && coBoSungChuaDuyet)
                        trangThai = 4;
                    else if (trangThaiGoc == 2 && !coBoSungChuaDuyet)
                        trangThai = 3;
                    else if (x.WorkflowType != null)
                        trangThai = 2;
                    else
                        trangThai = 1;

                    // Tổng ngân sách bổ sung
                    var tongBoSung = x.Approvals
                        .Where(a => a.IsSupplementary)
                        .Sum(a => a.NganSachBoSung ?? 0);

                    return new
                    {
                        TrangThai = trangThai,
                        DuToan = x.DuToan ?? 0,
                        TongBoSung = tongBoSung
                    };
                }).ToList();

                // ── Tính card tổng quan ──────────────────────────
                var tongNganSach = dataWithStatus
                    .Sum(x => x.DuToan + x.TongBoSung);

                var soHangMuc = dataWithStatus.Count;

                var hangMucChuaDuyet = dataWithStatus
                    .Count(x => x.TrangThai != 3);

                // ── Tính biểu đồ tròn ────────────────────────────
                // Chưa trình: TrangThai = 0, 1
                var tongChuaTrinh = dataWithStatus
                    .Where(x => x.TrangThai == 0
                             || x.TrangThai == 1)
                    .Sum(x => x.DuToan + x.TongBoSung);

                // Đang trình: TrangThai = 2, 4
                var tongDangTrinh = dataWithStatus
                    .Where(x => x.TrangThai == 2
                             || x.TrangThai == 4)
                    .Sum(x => x.DuToan + x.TongBoSung);

                // Đã phê duyệt: TrangThai = 3
                var tongDaPheduyet = dataWithStatus
                    .Where(x => x.TrangThai == 3)
                    .Sum(x => x.DuToan + x.TongBoSung);

                var result = new
                {
                    // Cards
                    TongNganSach = tongNganSach,
                    SoHangMuc = soHangMuc,
                    HangMucChuaDuyet = hangMucChuaDuyet,

                    // Biểu đồ tròn
                    TongChuaTrinh = tongChuaTrinh,
                    TongDangTrinh = tongDangTrinh,
                    TongDaPheduyet = tongDaPheduyet
                };

                return Json(new { success = true, data = result },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"GetDashboardData Error: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi khi tải dữ liệu."
                },
                    JsonRequestBehavior.AllowGet);
            }
        }
    }
}