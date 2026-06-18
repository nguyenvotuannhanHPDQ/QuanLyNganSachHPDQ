using PagedList;
using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using QuanLyNganSach.Models.Enums;
using QuanLyNganSach.Models.DTO;
using QuanLyNganSach.Hubs;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using static QuanLyNganSach.Models.ViewModels.BudgetRegistrationListViewModel;

namespace QuanLyNganSach.Controllers
{
    public class BudgetController : BaseController
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();
        private const int PageSize = 10;

        // GET: Budget/Index - Danh sách đăng ký ngân sách
        public ActionResult Index_OLD(int? page, string search, string sortOrder, int? phongBanId,int? filterTienDo, int? filterTrangThai)
        {
            try
            {
                int pageNumber = page ?? 1;

                // Validate sortOrder parameter
                var validSortOrders = new[] { "newest", "oldest", "budget-high", "budget-low" };
                if (string.IsNullOrWhiteSpace(sortOrder) || !validSortOrders.Contains(sortOrder))
                {
                    sortOrder = "newest"; // Default value
                }

                // Lấy thông tin user hiện tại
                var username = User.Identity.Name;
                if (string.IsNullOrWhiteSpace(username))
                {
                    TempData["Error"] = "Phiên đăng nhập đã hết hạn.";
                    return RedirectToAction("Login", "Account");
                }

                var currentUser = db.Users.FirstOrDefault(u => u.MaNhanVien == username);
                if (currentUser == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin người dùng.";
                    return RedirectToAction("Index", "Home");
                }

                // Kiểm tra quyền truy cập
                bool isManagerOrAdmin = currentUser.RoleId == Constants.RoleConst.Admin ||
                                        currentUser.RoleId == Constants.RoleConst.Manager;

                // Query danh sách đăng ký
                var query = from br in db.BudgetRegistrations
                            join pb in db.PhongBans on br.PhongBanId equals pb.PhongBanId
                            join u in db.Users on br.UserId equals u.UserId
                            select new
                            {
                                BudgetRegistration = br,
                                PhongBan = pb,
                                User = u
                            };

                // Apply role-based filtering
                // Thay đoạn Apply role-based filtering cũ
                if (!isManagerOrAdmin)
                {
                    // User thường xem được:
                    // 1. Hồ sơ của mình
                    // 2. Hồ sơ được phân nhiệm
                    query = query.Where(x =>
                        x.BudgetRegistration.UserId == currentUser.UserId
                        || x.BudgetRegistration.BudgetRegistrationPhanNhiems
                            .Any(p => p.UserId == currentUser.UserId));
                }

                // Apply department filter (chỉ Manager/Admin mới có filter này)
                if (isManagerOrAdmin && phongBanId.HasValue && phongBanId.Value > 0)
                {
                    query = query.Where(x => x.BudgetRegistration.PhongBanId == phongBanId.Value);
                }

                // Project to ViewModel
                var viewModelQuery = query.Select(x => new BudgetRegistrationListViewModel
                {
                    BudgetRegistrationId = x.BudgetRegistration.BudgetRegistrationId,
                    MaHangMuc = x.BudgetRegistration.MaHangMuc,
                    TenHangMuc = x.BudgetRegistration.TenHangMuc,

                    DuToan = x.BudgetRegistration.DuToan ?? 0,

                    SoToTrinh = x.BudgetRegistration.SoToTrinh ?? string.Empty,
                    LyDoDauTu = x.BudgetRegistration.LyDoDauTu ?? string.Empty,
                    MoTaKyThuat = x.BudgetRegistration.MoTaKyThuat ?? string.Empty,

                    NgayBatDau = x.BudgetRegistration.NgayBatDau,
                    NgayKetThuc = x.BudgetRegistration.NgayKetThuc,

                    TenPhongBan = x.PhongBan.TenPhongBan,
                    PhongBanId = x.PhongBan.PhongBanId,

                    NguoiDangKy = x.User.HoTen,
                    NgayTao = x.BudgetRegistration.CreatedDate,

                    // *** THÊM MỚI ***
                    SoToTrinhRaw = x.BudgetRegistration.SoToTrinh,
                    WorkflowType = x.BudgetRegistration.WorkflowType,

                    // TrangThaiPheDuyet của record Ngân sách gốc (IsSupplementary = false)
                    TrangThaiPheDuyetGoc = x.BudgetRegistration.BudgetApprovals
                    .Where(a => !a.IsSupplementary)
                    .Select(a => (int?)a.TrangThaiPheDuyet)
                    .FirstOrDefault() ?? 0,
                    // Có ít nhất 1 đợt bổ sung chưa duyệt (TrangThaiPheDuyet != 2)
                    CoBoSungChuaDuyet = x.BudgetRegistration.BudgetApprovals
                    .Any(a => a.IsSupplementary && a.TrangThaiPheDuyet != 2),
                    // Có ít nhất 1 đợt bổ sung đã duyệt (TrangThaiPheDuyet = 2)
                    CoBoSungDaDuyet = x.BudgetRegistration.BudgetApprovals
                    .Any(a => a.IsSupplementary && a.TrangThaiPheDuyet == 2),

                    TongTienDo = x.BudgetRegistration.ProgressConfigs
                        .Select(p => (decimal?)p.TongTienDo)
                        .FirstOrDefault(),

                    DanhGiaChung = x.BudgetRegistration.ProgressConfigs
                        .Select(p => p.DanhGiaChung)
                        .FirstOrDefault(),

                    // *** THÊM MỚI ***

                    // Có thông tin phê duyệt không
                    // (tồn tại ít nhất 1 record BudgetApprovals có TrangThaiPheDuyet = 2)
                    CoThongTinPheDuyet = x.BudgetRegistration.BudgetApprovals
                    .Any(a => a.TrangThaiPheDuyet == 2),

                    // Tổng tiền đã duyệt:
                    // Ngân sách gốc đã duyệt (IsSupplementary=false, TrangThaiPheDuyet=2)
                    // + Tổng ngân sách bổ sung đã duyệt (IsSupplementary=true, TrangThaiPheDuyet=2)
                    TongTienDaDuyet =
                    // Ngân sách gốc đã duyệt → lấy DuToanPheDuyet
                    (x.BudgetRegistration.BudgetApprovals
                        .Where(a => !a.IsSupplementary
                                 && a.TrangThaiPheDuyet == 2)
                        .Select(a => (decimal?)a.DuToanPheDuyet)
                        .FirstOrDefault() ?? 0)
                    +
                    // Các đợt bổ sung đã duyệt → lấy NganSachBoSung
                    (x.BudgetRegistration.BudgetApprovals
                        .Where(a => a.IsSupplementary
                                 && a.TrangThaiPheDuyet == 2)
                        .Sum(a => (decimal?)a.NganSachBoSung) ?? 0),
                    UserId = x.BudgetRegistration.UserId,
                    // *** THÊM MỚI ***
                    IsPhanNhiemUser = x.BudgetRegistration.UserId != currentUser.UserId
                        && !isManagerOrAdmin
                        && x.BudgetRegistration.BudgetRegistrationPhanNhiems
                            .Any(p => p.UserId == currentUser.UserId),
                });

                // *** THÊM MỚI: Filter theo tiến độ (DanhGiaChung) ***
                // filterTienDo = -1 nghĩa là "Chưa xác định" (NULL)
                if (filterTienDo.HasValue)
                {
                    if (filterTienDo.Value == -1)
                    {
                        // Chưa xác định: DanhGiaChung IS NULL
                        viewModelQuery = viewModelQuery
                            .Where(b => b.DanhGiaChung == null);
                    }
                    else
                    {
                        viewModelQuery = viewModelQuery
                            .Where(b => b.DanhGiaChung == filterTienDo.Value);
                    }
                }

                // *** THÊM MỚI: Filter theo trạng thái hồ sơ ***
                // Chuyển logic TrangThaiHienThi sang điều kiện SQL trực tiếp
                if (filterTrangThai.HasValue)
                {
                    switch (filterTrangThai.Value)
                    {
                        case 0: // Chưa có chủ trương: SoToTrinh IS NULL
                            viewModelQuery = viewModelQuery
                                .Where(b => b.SoToTrinhRaw == null
                                         || b.SoToTrinhRaw == "");
                            break;

                        case 1: // Đăng ký mới: SoToTrinh != NULL && WorkflowType IS NULL
                            viewModelQuery = viewModelQuery
                                .Where(b => (b.SoToTrinhRaw != null
                                          && b.SoToTrinhRaw != "")
                                         && b.WorkflowType == null);
                            break;

                        case 2: // Đang thực hiện xin ngân sách:
                                // WorkflowType != NULL && != 2 && != 3
                                // && TrangThaiPheDuyetGoc != 2
                            viewModelQuery = viewModelQuery
                                .Where(b => b.WorkflowType != null
                                         && b.WorkflowType != 2
                                         && b.WorkflowType != 3
                                         && b.TrangThaiPheDuyetGoc != 2
                                         && (b.SoToTrinhRaw != null
                                          && b.SoToTrinhRaw != ""));
                            break;

                        case 3: // Đã phê duyệt:
                                // TrangThaiPheDuyetGoc = 2 && !CoBoSungChuaDuyet
                            viewModelQuery = viewModelQuery
                                .Where(b => b.TrangThaiPheDuyetGoc == 2
                                         && !b.CoBoSungChuaDuyet);
                            break;

                        case 4: // Đang bổ sung:
                                // TrangThaiPheDuyetGoc = 2 && CoBoSungChuaDuyet
                            viewModelQuery = viewModelQuery
                                .Where(b => b.TrangThaiPheDuyetGoc == 2
                                         && b.CoBoSungChuaDuyet);
                            break;

                        case 5: // Theo luồng chi phí SX: WorkflowType = 2
                            viewModelQuery = viewModelQuery
                                .Where(b => b.WorkflowType == 2);
                            break;

                        case 6: // Chưa đủ hồ sơ: WorkflowType = 3
                            viewModelQuery = viewModelQuery
                                .Where(b => b.WorkflowType == 3);
                            break;
                    }
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    viewModelQuery = viewModelQuery.Where(b =>
                        (b.MaHangMuc != null && b.MaHangMuc.ToLower().Contains(search)) ||
                        (b.TenHangMuc != null && b.TenHangMuc.ToLower().Contains(search)) ||
                        (b.SoToTrinh != null && b.SoToTrinh.ToLower().Contains(search)) ||
                        (b.TenPhongBan != null && b.TenPhongBan.ToLower().Contains(search)) ||
                        (b.NguoiDangKy != null && b.NguoiDangKy.ToLower().Contains(search)) ||
                        (b.LyDoDauTu != null && b.LyDoDauTu.ToLower().Contains(search)));
                }

                // Apply sorting
                IOrderedQueryable<BudgetRegistrationListViewModel> orderedQuery;

                switch (sortOrder)
                {
                    case "oldest":
                        orderedQuery = viewModelQuery.OrderBy(b => b.NgayTao);
                        break;

                    case "budget-high":
                        orderedQuery = viewModelQuery.OrderByDescending(b => b.DuToan)
                                                     .ThenByDescending(b => b.NgayTao);
                        break;

                    case "budget-low":
                        orderedQuery = viewModelQuery.OrderBy(b => b.DuToan)
                                                     .ThenByDescending(b => b.NgayTao);
                        break;

                    case "newest":
                    default:
                        orderedQuery = viewModelQuery.OrderByDescending(b => b.NgayTao);
                        break;
                }

                // Apply pagination
                var budgetList = orderedQuery.ToPagedList(pageNumber, PageSize);

                // Prepare department list for Manager/Admin
                if (isManagerOrAdmin)
                {
                    var departments = db.PhongBans
                        .Where(pb => pb.IsActive == true) // Chỉ lấy phòng ban đang hoạt động
                        .OrderBy(pb => pb.TenPhongBan)
                        .Select(pb => new SelectListItem
                        {
                            Value = pb.PhongBanId.ToString(),
                            Text = pb.TenPhongBan,
                            Selected = phongBanId.HasValue && pb.PhongBanId == phongBanId.Value
                        })
                        .ToList();

                    // Thêm option "Tất cả phòng ban" ở đầu
                    departments.Insert(0, new SelectListItem
                    {
                        Value = "",
                        Text = "-- Tất cả đơn vị --",
                        Selected = !phongBanId.HasValue
                    });

                    ViewBag.PhongBanList = departments;
                }

                // Pass parameters to view
                ViewBag.CurrentSearch = search ?? string.Empty;
                ViewBag.CurrentSort = sortOrder;
                ViewBag.CurrentPhongBanId = phongBanId;
                ViewBag.IsManagerOrAdmin = isManagerOrAdmin;
                ViewBag.CurrentFilterTienDo = filterTienDo;
                ViewBag.CurrentFilterTrangThai = filterTrangThai;

                // *** THÊM MỚI: Truyền dropdown Phòng ban + Chức năng xuống View cho modal ***
                ViewBag.DsPhongBanModal = db.PhongBans
                    .OrderBy(p => p.TenPhongBan)
                    .Select(p => new { value = p.PhongBanId.ToString(), text = p.TenPhongBan })
                    .ToList();

                ViewBag.DsChucNangModal = db.ChucNang_NhiemVu
                    .OrderBy(c => c.TenChucNang)
                    .Select(c => new { value = c.ChucNangNhiemVuId.ToString(), text = c.TenChucNang })
                    .ToList();

                ViewBag.DsKhuVucModal = db.ProjectAreas
                    .OrderBy(p => p.AreaName)
                    .Select(p => new SelectListItem
                    {
                        Value = p.ProjectAreaId.ToString(),
                        Text = p.AreaName
                    })
                    .ToList();

                ViewBag.CurrentUserId = currentUser.UserId;

                ViewBag.DsCategory = GetCategoryTypes();
                ViewBag.DsPriority = GetPriorityLevels();

                return View(budgetList);
            }
            catch (DbEntityValidationException ex)
            {
                // Log detailed validation errors
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                TempData["Error"] = "Lỗi xác thực dữ liệu. Vui lòng liên hệ quản trị viên.";

                return View(new PagedList<BudgetRegistrationListViewModel>(
                    Enumerable.Empty<BudgetRegistrationListViewModel>().AsQueryable(), 1, PageSize));
            }
            catch (Exception ex)
            {
                // Log general error
                System.Diagnostics.Debug.WriteLine($"Error in Budget/Index: {ex.Message}");

                TempData["Error"] = "Đã xảy ra lỗi khi tải danh sách đăng ký. Vui lòng thử lại.";

                return View(new PagedList<BudgetRegistrationListViewModel>(
                    Enumerable.Empty<BudgetRegistrationListViewModel>().AsQueryable(), 1, PageSize));
            }
        }

        public ActionResult Index(int? page, string search, string sortOrder, int? phongBanId, int? filterTienDo, int? filterTrangThai)
        {
            try
            {
                int pageNumber = page ?? 1;

                // Validate sortOrder parameter
                var validSortOrders = new[] { "newest", "oldest", "budget-high", "budget-low" };
                if (string.IsNullOrWhiteSpace(sortOrder) || !validSortOrders.Contains(sortOrder))
                {
                    sortOrder = "newest"; // Default value
                }

                // Lấy thông tin user hiện tại
                var username = User.Identity.Name;
                if (string.IsNullOrWhiteSpace(username))
                {
                    TempData["Error"] = "Phiên đăng nhập đã hết hạn.";
                    return RedirectToAction("Login", "Account");
                }

                var currentUser = db.Users.FirstOrDefault(u => u.MaNhanVien == username);
                if (currentUser == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin người dùng.";
                    return RedirectToAction("Index", "Home");
                }

                // Kiểm tra quyền truy cập
                bool isManagerOrAdmin = currentUser.RoleId == Constants.RoleConst.Admin ||
                                        currentUser.RoleId == Constants.RoleConst.Manager;

                // Query danh sách đăng ký ban đầu
                var query = from br in db.BudgetRegistrations
                            join pb in db.PhongBans on br.PhongBanId equals pb.PhongBanId
                            join u in db.Users on br.UserId equals u.UserId
                            select new
                            {
                                BudgetRegistration = br,
                                PhongBan = pb,
                                User = u
                            };

                // Apply role-based filtering
                if (!isManagerOrAdmin)
                {
                    // User thường xem được: Hồ sơ của mình HOẶC Hồ sơ được phân nhiệm
                    query = query.Where(x =>
                        x.BudgetRegistration.UserId == currentUser.UserId
                        || x.BudgetRegistration.BudgetRegistrationPhanNhiems
                            .Any(p => p.UserId == currentUser.UserId));
                }

                // Apply department filter (chỉ Manager/Admin mới có filter này)
                if (isManagerOrAdmin && phongBanId.HasValue && phongBanId.Value > 0)
                {
                    query = query.Where(x => x.BudgetRegistration.PhongBanId == phongBanId.Value);
                }

                // Project to ViewModel
                var viewModelQuery = query.Select(x => new BudgetRegistrationListViewModel
                {
                    BudgetRegistrationId = x.BudgetRegistration.BudgetRegistrationId,
                    MaHangMuc = x.BudgetRegistration.MaHangMuc,
                    TenHangMuc = x.BudgetRegistration.TenHangMuc,
                    DuToan = x.BudgetRegistration.DuToan ?? 0,
                    SoToTrinh = x.BudgetRegistration.SoToTrinh ?? string.Empty,
                    LyDoDauTu = x.BudgetRegistration.LyDoDauTu ?? string.Empty,
                    MoTaKyThuat = x.BudgetRegistration.MoTaKyThuat ?? string.Empty,
                    NgayBatDau = x.BudgetRegistration.NgayBatDau,
                    NgayKetThuc = x.BudgetRegistration.NgayKetThuc,
                    TenPhongBan = x.PhongBan.TenPhongBan,
                    PhongBanId = x.PhongBan.PhongBanId,
                    NguoiDangKy = x.User.HoTen,
                    NgayTao = x.BudgetRegistration.CreatedDate,

                    // DỮ LIỆU THÔ PHỤC VỤ THUỘC TÍNH GET TỰ ĐỘNG TÍNH TOÁN TRẠNG THÁI
                    SoToTrinhRaw = x.BudgetRegistration.SoToTrinh,
                    WorkflowType = x.BudgetRegistration.WorkflowType,

                    BudgetApprovals = x.BudgetRegistration.BudgetApprovals.Select(a => new BudgetApprovalRawField
                    {
                        IsSupplementary = a.IsSupplementary,
                        NgayDuyetPDA = a.NgayDuyetPDA,
                        NgayDuyetBGD = a.NgayDuyetBGD
                    }).ToList(),

                    // TIẾN ĐỘ HẠNG MỤC
                    TongTienDo = x.BudgetRegistration.ProgressConfigs
                        .Select(p => (decimal?)p.TongTienDo)
                        .FirstOrDefault(),
                    DanhGiaChung = x.BudgetRegistration.ProgressConfigs
                        .Select(p => p.DanhGiaChung)
                        .FirstOrDefault(),

                    // THÔNG TIN TỔNG HỢP KINH PHÍ ĐÃ DUYỆT KHÁC
                    CoThongTinPheDuyet = x.BudgetRegistration.BudgetApprovals.Any(a => a.TrangThaiPheDuyet == 2),
                    TongTienDaDuyet = (x.BudgetRegistration.BudgetApprovals
                                        .Where(a => !a.IsSupplementary && a.TrangThaiPheDuyet == 2)
                                        .Select(a => (decimal?)a.DuToanPheDuyet)
                                        .FirstOrDefault() ?? 0)
                                    + (x.BudgetRegistration.BudgetApprovals
                                        .Where(a => a.IsSupplementary && a.TrangThaiPheDuyet == 2)
                                        .Sum(a => (decimal?)a.NganSachBoSung) ?? 0),
                    UserId = x.BudgetRegistration.UserId,
                    IsPhanNhiemUser = x.BudgetRegistration.UserId != currentUser.UserId
                        && !isManagerOrAdmin
                        && x.BudgetRegistration.BudgetRegistrationPhanNhiems.Any(p => p.UserId == currentUser.UserId),
                });

                // Apply filter theo tiến độ (DanhGiaChung)
                if (filterTienDo.HasValue)
                {
                    if (filterTienDo.Value == -1)
                    {
                        viewModelQuery = viewModelQuery.Where(b => b.DanhGiaChung == null);
                    }
                    else
                    {
                        viewModelQuery = viewModelQuery.Where(b => b.DanhGiaChung == filterTienDo.Value);
                    }
                }

                // TÁI CẤU TRÚC: Filter theo bộ mã trạng thái hồ sơ mới trực tiếp trên SQL
                if (filterTrangThai.HasValue)
                {
                    switch (filterTrangThai.Value)
                    {
                        case 0: // Đăng ký mới (Dựa theo cấu trúc Excel Nhóm 1: WorkflowType == null)
                            viewModelQuery = viewModelQuery.Where(b => b.WorkflowType == null);
                            break;

                        case 1: // Chưa đủ hồ sơ (Gồm Case 2.1: WorkflowType == 4 HOẶC Case 4.1: WorkflowType == 1, không bổ sung, gốc chưa duyệt PDA & BGD)
                            viewModelQuery = viewModelQuery.Where(b =>
                                b.WorkflowType == 4 ||
                                (b.WorkflowType == 1 &&
                                 !b.BudgetApprovals.Any(a => a.IsSupplementary) &&
                                 b.BudgetApprovals.Any(a => !a.IsSupplementary && a.NgayDuyetPDA == null && a.NgayDuyetBGD == null))
                            );
                            break;

                        case 2: // Theo luồng chi phí sản xuất (Nhóm 3: WorkflowType == 2)
                            viewModelQuery = viewModelQuery.Where(b => b.WorkflowType == 2);
                            break;

                        case 3: // Đang thực hiện xin ngân sách (Case 4.2: WorkflowType == 1, không bổ sung, gốc có duyệt PDA nhưng chưa duyệt BGD)
                            viewModelQuery = viewModelQuery.Where(b =>
                                b.WorkflowType == 1 &&
                                !b.BudgetApprovals.Any(a => a.IsSupplementary) &&
                                b.BudgetApprovals.Any(a => !a.IsSupplementary && a.NgayDuyetPDA != null && a.NgayDuyetBGD == null)
                            );
                            break;

                        case 4: // Đăng bổ sung ngân sách (Case 4.5: WorkflowType == 1, có bổ sung thỏa mãn duyệt PDA nhưng chưa duyệt BGD)
                            viewModelQuery = viewModelQuery.Where(b =>
                                b.WorkflowType == 1 &&
                                b.BudgetApprovals.Any(a => a.IsSupplementary && a.NgayDuyetPDA != null && a.NgayDuyetBGD == null)
                            );
                            break;

                        case 5: // Đã phê duyệt ngân sách (Gồm Case 4.3 gốc đã duyệt xong, Case 4.4 bổ sung chưa duyệt gì, Case 4.6 bổ sung duyệt xong hết)
                            viewModelQuery = viewModelQuery.Where(b =>
                                b.WorkflowType == 1 &&
                                (
                                    // Case 4.3: Không có bổ sung và gốc đã duyệt cả 2
                                    (!b.BudgetApprovals.Any(a => a.IsSupplementary) && b.BudgetApprovals.Any(a => !a.IsSupplementary && a.NgayDuyetPDA != null && a.NgayDuyetBGD != null)) ||
                                    // Case 4.4: Có đợt bổ sung nhưng đợt bổ sung đó chưa tác động duyệt gì cả
                                    b.BudgetApprovals.Any(a => a.IsSupplementary && a.NgayDuyetPDA == null && a.NgayDuyetBGD == null) ||
                                    // Case 4.6: Có đợt bổ sung đã hoàn tất duyệt cả 2
                                    b.BudgetApprovals.Any(a => a.IsSupplementary && a.NgayDuyetPDA != null && a.NgayDuyetBGD != null)
                                )
                            );
                            break;
                    }
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    viewModelQuery = viewModelQuery.Where(b =>
                        (b.MaHangMuc != null && b.MaHangMuc.ToLower().Contains(search)) ||
                        (b.TenHangMuc != null && b.TenHangMuc.ToLower().Contains(search)) ||
                        (b.SoToTrinh != null && b.SoToTrinh.ToLower().Contains(search)) ||
                        (b.TenPhongBan != null && b.TenPhongBan.ToLower().Contains(search)) ||
                        (b.NguoiDangKy != null && b.NguoiDangKy.ToLower().Contains(search)) ||
                        (b.LyDoDauTu != null && b.LyDoDauTu.ToLower().Contains(search)));
                }

                // Apply sorting
                IOrderedQueryable<BudgetRegistrationListViewModel> orderedQuery;
                switch (sortOrder)
                {
                    case "oldest":
                        orderedQuery = viewModelQuery.OrderBy(b => b.NgayTao);
                        break;
                    case "budget-high":
                        orderedQuery = viewModelQuery.OrderByDescending(b => b.DuToan).ThenByDescending(b => b.NgayTao);
                        break;
                    case "budget-low":
                        orderedQuery = viewModelQuery.OrderBy(b => b.DuToan).ThenByDescending(b => b.NgayTao);
                        break;
                    case "newest":
                    default:
                        orderedQuery = viewModelQuery.OrderByDescending(b => b.NgayTao);
                        break;
                }

                // Apply pagination
                var budgetList = orderedQuery.ToPagedList(pageNumber, PageSize);

                // Prepare department list for Manager/Admin
                if (isManagerOrAdmin)
                {
                    var departments = db.PhongBans
                        .Where(pb => pb.IsActive == true)
                        .OrderBy(pb => pb.TenPhongBan)
                        .Select(pb => new SelectListItem
                        {
                            Value = pb.PhongBanId.ToString(),
                            Text = pb.TenPhongBan,
                            Selected = phongBanId.HasValue && pb.PhongBanId == phongBanId.Value
                        })
                        .ToList();

                    departments.Insert(0, new SelectListItem
                    {
                        Value = "",
                        Text = "-- Tất cả đơn vị --",
                        Selected = !phongBanId.HasValue
                    });

                    ViewBag.PhongBanList = departments;
                }

                // Pass parameters to view
                ViewBag.CurrentSearch = search ?? string.Empty;
                ViewBag.CurrentSort = sortOrder;
                ViewBag.CurrentPhongBanId = phongBanId;
                ViewBag.IsManagerOrAdmin = isManagerOrAdmin;
                ViewBag.CurrentFilterTienDo = filterTienDo;
                ViewBag.CurrentFilterTrangThai = filterTrangThai;

                // Binds Dropdowns cho Modal
                ViewBag.DsPhongBanModal = db.PhongBans.OrderBy(p => p.TenPhongBan).Select(p => new { value = p.PhongBanId.ToString(), text = p.TenPhongBan }).ToList();
                ViewBag.DsChucNangModal = db.ChucNang_NhiemVu.OrderBy(c => c.TenChucNang).Select(c => new { value = c.ChucNangNhiemVuId.ToString(), text = c.TenChucNang }).ToList();
                ViewBag.DsKhuVucModal = db.ProjectAreas.OrderBy(p => p.AreaName).Select(p => new SelectListItem { Value = p.ProjectAreaId.ToString(), Text = p.AreaName }).ToList();

                ViewBag.CurrentUserId = currentUser.UserId;
                ViewBag.DsCategory = GetCategoryTypes();
                ViewBag.DsPriority = GetPriorityLevels();
                ViewBag.DsInvestmentReasons = GetInvestmentReasonSelectList();

                return View(budgetList);
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                TempData["Error"] = "Lỗi xác thực dữ liệu. Vui lòng liên hệ quản trị viên.";
                return View(new PagedList<BudgetRegistrationListViewModel>(Enumerable.Empty<BudgetRegistrationListViewModel>().AsQueryable(), 1, PageSize));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Budget/Index: {ex.Message}");
                TempData["Error"] = "Đã xảy ra lỗi khi tải danh sách đăng ký. Vui lòng thử lại.";
                return View(new PagedList<BudgetRegistrationListViewModel>(Enumerable.Empty<BudgetRegistrationListViewModel>().AsQueryable(), 1, PageSize));
            }
        }

        public ActionResult Create()
        {
            // KIỂM TRA ĐỢT ĐĂNG KÝ
            if (!IsRegistrationOpen())
            {
                TempData["Error"] = "Hiện tại hệ thống đang đóng đợt đăng ký. Vui lòng liên hệ Admin để biết thêm chi tiết.";
                return RedirectToAction("Index");
            }

            try
            {
                // Create view model with default values
                var model = new CreateBudgetRegistrationViewModel
                {
                    // Generate MaHangMuc with default CategoryTypeId = 1
                    //MaHangMuc = GenerateMaHangMuc(1),

                    // Set default CategoryTypeId
                    CategoryTypeId = 1,
                    PriorityLevelId = 1,
                    SoLuong = 1,

                    // Load dropdown lists
                    CategoryTypes = GetCategoryTypes(),
                    PriorityLevels = GetPriorityLevels(),
                    InvestmentReasons = GetInvestmentReasonSelectList(),

                    // Set default dates
                    NgayBatDau = DateTime.Today,
                    NgayKetThuc = DateTime.Today,

                    // *** THÊM MỚI ***
                    DanhSachPhongBan = GetPhongBanSelectList(),
                    DanhSachChucNang = GetChucNangSelectList(),
                    DanhSachPhanNhiem = new List<PhanNhiemViewModel>(),
                    DanhSachKhuVuc = GetProjectAreaSelectList()

                };

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create GET Error: {ex.Message}");
                TempData["Error"] = "Đã xảy ra lỗi khi tải form đăng ký. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }
        }

        private bool IsRegistrationOpen()
        {
            // 1. Đặc quyền Admin/Manager: Luôn mở
            if (CurrentUser.RoleId == Constants.RoleConst.Admin || CurrentUser.RoleId == Constants.RoleConst.Manager)
                return true;

            // 2. Lấy cấu hình duy nhất
            var config = db.SystemRegistrationPeriods.FirstOrDefault();

            // 3. Logic mặc định: Nếu chưa có cấu hình thì luôn Mở
            if (config == null) return true;

            // 4. Nếu có cấu hình: Kiểm tra IsActive và Khoảng thời gian
            var now = DateTime.Now;
            if ((bool) config.IsActive)
            {
                return now >= config.StartDate && now <= config.EndDate;
            }

            return false; // Trường hợp IsActive = false
        }

        [HttpGet]
        public ActionResult Config() // Tên Action tùy bạn đặt theo menu Admin
        {
            // Check permissions
            bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                    CurrentUser.RoleId == Constants.RoleConst.Manager;

            if (!isManagerOrAdmin)
            {
                TempData["Error"] = "Bạn không có quyền thực hiện chức năng này.";
                return RedirectToAction("Index");
            }
            // Lấy cấu hình duy nhất
            var config = db.SystemRegistrationPeriods.FirstOrDefault();

            // Nếu chưa có dữ liệu, khởi tạo model mặc định để tránh lỗi null ở View
            var model = new SystemConfigViewModel
            {
                StartDate = config?.StartDate ?? DateTime.Today,
                EndDate = config?.EndDate ?? DateTime.Today.AddMonths(1),
                IsActive = config?.IsActive ?? true
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveSystemConfig(SystemConfigViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu nhập vào không hợp lệ.";
                return RedirectToAction("Config");
            }

            if (model.EndDate < model.StartDate)
            {
                TempData["Error"] = "Ngày kết thúc không được nhỏ hơn ngày bắt đầu.";
                return RedirectToAction("Config");
            }

            var config = db.SystemRegistrationPeriods.FirstOrDefault();
            if (config == null)
            {
                config = new SystemRegistrationPeriod { PeriodId = 1, PeriodName = "System Config" };
                db.SystemRegistrationPeriods.Add(config);
            }

            config.StartDate = model.StartDate;
            config.EndDate = model.EndDate;
            config.IsActive = model.IsActive;
            config.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            TempData["Success"] = "Cập nhật cấu hình hệ thống thành công!";
            return RedirectToAction("Config");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateBudgetRegistrationViewModel model)
        {
            if (!IsRegistrationOpen())
            {
                TempData["Error"] = "Hành động bị từ chối. Đợt đăng ký đã đóng.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ReloadDropdowns(model);
                return View(model);
            }

            if (model.NgayKetThuc < model.NgayBatDau)
            {
                ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc phải >= ngày bắt đầu");
                ReloadDropdowns(model);
                return View(model);
            }

            // Xác định trạng thái hồ sơ (Đủ thông tin hay Thiếu thông tin)
            int trangThaiHoSo = DetermineHoSoStatus(model);

            bool isManualReason = false;
            if (model.InvestmentReasonId.HasValue)
            {
                // Tìm bản ghi danh mục trong DB theo ID được chọn
                var reasonCategory = db.BudgetInvestmentReasons.Find(model.InvestmentReasonId.Value);
                if (reasonCategory != null && reasonCategory.IsManual)
                {
                    isManualReason = true;
                }
            }

            // 2. Chuẩn hóa dữ liệu trước khi lưu dựa trên loại lý do đầu tư
            int? finalInvestmentReasonId = model.InvestmentReasonId;
            string finalLyDoDauTuText = null;

            if (isManualReason)
            {
                // Nếu là "Điền thủ công": Khóa ngoại bằng NULL, lưu chuỗi nhập tay từ textarea
                finalInvestmentReasonId = null;
                finalLyDoDauTuText = model.LyDoDauTu?.Trim();
            }
            else
            {
                // Nếu chọn các option danh mục khác: Lưu khóa ngoại, text nhập tay sẽ để trống (hoặc lưu tên danh mục tùy bạn)
                finalLyDoDauTuText = null;
            }

            // Create entity from view model
            var entity = new BudgetRegistration
            {
                //MaHangMuc = model.MaHangMuc,
                MaHangMuc = null,
                TenHangMuc = model.TenHangMuc?.Trim(),
                DuToan = model.DuToan,
                SoToTrinh = model.SoToTrinh?.Trim(),
                SoLuong = model.SoLuong,
                CategoryTypeId = model.CategoryTypeId,
                PriorityLevelId = model.PriorityLevelId,
                InvestmentReasonId = finalInvestmentReasonId,
                LyDoDauTu = finalLyDoDauTuText,
                MoTaKyThuat = model.MoTaKyThuat?.Trim(),
                LinkTaiLieuLienQuan = model.LinkTaiLieuLienQuan?.Trim(),
                NgayBatDau = model.NgayBatDau,
                NgayKetThuc = model.NgayKetThuc,
                UserId = CurrentUser.UserId,
                PhongBanId = CurrentUser.PhongBanId,
                TrangThai = trangThaiHoSo,
                CreatedDate = DateTime.Now,
                ProjectAreaId = model.ProjectAreaId
            };

            db.BudgetRegistrations.Add(entity);
            db.SaveChanges();

            // *** THÊM MỚI: Validate Phân nhiệm phía server ***
            if (model.DanhSachPhanNhiem == null
             || !model.DanhSachPhanNhiem.Any(p => p.PhongBanId != null))
            {
                ModelState.AddModelError("",
                    "Vui lòng thêm ít nhất một dòng phân nhiệm.");
                ReloadDropdowns(model);
                return View(model);
            }

            var dongThieuInfo = model.DanhSachPhanNhiem
                .Where(p => p.PhongBanId != null)
                .Select((p, i) => new {
                    Index = i + 1,
                    ThieuChucNang = string.IsNullOrEmpty(p.TenChucNangNhapTay)
                                 && p.ChucNangNhiemVuId == null,
                    ThieuEmail = string.IsNullOrEmpty(p.Email?.Trim()),
                    ThieuUser = p.UserId == null
                })
                .Where(p => p.ThieuChucNang || p.ThieuEmail)
                .ToList();

            if (dongThieuInfo.Any())
            {
                foreach (var dong in dongThieuInfo)
                {
                    if (dong.ThieuChucNang)
                        ModelState.AddModelError("",
                            $"Dòng {dong.Index}: thiếu Chức năng / Nhiệm vụ.");
                    if (dong.ThieuEmail)
                        ModelState.AddModelError("",
                            $"Dòng {dong.Index}: thiếu Email liên hệ.");
                    if (dong.ThieuUser)
                        ModelState.AddModelError("",
                            $"Dòng {dong.Index}: thiếu thông tin Nhân viên.");
                }
                ReloadDropdowns(model);
                return View(model);
            }

            // *** THÊM MỚI: Lưu các dòng Phân nhiệm ***
            if (model.DanhSachPhanNhiem != null && model.DanhSachPhanNhiem.Any())
            {
                foreach (var pn in model.DanhSachPhanNhiem)
                {
                    // Bỏ qua dòng trống (không chọn phòng ban)
                    if (pn.PhongBanId == null) continue;

                    var phanNhiem = new BudgetRegistrationPhanNhiem
                    {
                        BudgetRegistrationId = entity.BudgetRegistrationId,
                        PhongBanId = pn.PhongBanId.Value,
                        ChucNangNhiemVuId = pn.ChucNangNhiemVuId,
                        TenChucNangNhapTay = pn.TenChucNangNhapTay?.Trim(),
                        Email = pn.Email?.Trim(),
                        GhiChu = pn.GhiChu?.Trim(),
                        UserId = pn.UserId
                    };

                    db.BudgetRegistrationPhanNhiems.Add(phanNhiem);
                }

                db.SaveChanges(); // SaveChanges lần 2 cho Phân nhiệm
            }

            var files = Request.Files.GetMultiple("HoSoCanCu")
                         .Where(f => f != null && f.ContentLength > 0)
                         .ToList();
            if (files.Any())
            {
                SaveHoSoCanCuAndAttachment(files, entity.BudgetRegistrationId);
            }

            // THÊM MỚI: gửi thông báo đến tất cả Admin
            var adminUsers = db.Users
                               .Where(u => u.RoleId == 1)
                               .ToList();

            foreach (var admin in adminUsers)
            {
                NotificationHelper.Send(
                    db,
                    toUserId: admin.UserId,
                    title: "Hồ sơ đăng ký mới",
                    message: $"{CurrentUser.TenPhongBan} đã đăng ký hồ sơ \"{entity.TenHangMuc}\". Vui lòng xem xét và xác nhận.",
                    url: $"/Budget/Index?openBudget={entity.BudgetRegistrationId}",
                    relatedRevisionId: entity.BudgetRegistrationId
                );
            }

            TempData["Success"] = "Đăng ký hồ sơ ngân sách thành công";
            return RedirectToAction("Create");
        }

        //[HttpPost]
        //public ActionResult Delete(int id)
        //{
        //    try
        //    {
        //        if (CurrentUser == null)
        //            return Json(new
        //            {
        //                success = false,
        //                message = "Phiên đăng nhập đã hết hạn."
        //            });

        //        var entity = db.BudgetRegistrations
        //            .FirstOrDefault(x => x.BudgetRegistrationId == id);

        //        if (entity == null)
        //            return Json(new
        //            {
        //                success = false,
        //                message = "Không tìm thấy hồ sơ."
        //            });

        //        bool isManagerOrAdmin =
        //            CurrentUser.RoleId == Constants.RoleConst.Admin ||
        //            CurrentUser.RoleId == Constants.RoleConst.Manager;

        //        // Kiểm tra quyền xóa
        //        if (!isManagerOrAdmin)
        //        {
        //            // User thường chỉ xóa được hồ sơ của mình
        //            // và chỉ khi chưa xác nhận luồng
        //            if (entity.UserId != CurrentUser.UserId)
        //                return Json(new
        //                {
        //                    success = false,
        //                    message = "Bạn không có quyền xóa hồ sơ này."
        //                });

        //            if (entity.WorkflowType != null)
        //                return Json(new
        //                {
        //                    success = false,
        //                    message = "Hồ sơ đã được xác nhận, "
        //                            + "không thể xóa."
        //                });
        //        }

        //        // ── Xóa bảng con theo thứ tự ────────────────────────────

        //        // 1. ProgressAreaItems → ProgressAreas
        //        var progressAreas = db.ProgressAreas
        //            .Include(a => a.ProgressAreaItems)
        //            .Where(a => a.BudgetRegistrationId == id)
        //            .ToList();
        //        foreach (var area in progressAreas)
        //            db.ProgressAreaItems.RemoveRange(area.ProgressAreaItems);
        //        db.ProgressAreas.RemoveRange(progressAreas);

        //        // 2. ProgressConfigs
        //        var progressConfigs = db.ProgressConfigs
        //            .Where(p => p.BudgetRegistrationId == id).ToList();
        //        db.ProgressConfigs.RemoveRange(progressConfigs);

        //        // 3. BudgetApprovals
        //        var approvals = db.BudgetApprovals
        //            .Where(a => a.BudgetRegistrationId == id).ToList();
        //        db.BudgetApprovals.RemoveRange(approvals);

        //        // 4. BudgetAttachments
        //        var attachments = db.BudgetAttachments
        //            .Where(a => a.BudgetRegistrationId == id).ToList();
        //        db.BudgetAttachments.RemoveRange(attachments);

        //        // 5. BudgetRegistrationPhanNhiem
        //        var phanNhiems = db.BudgetRegistrationPhanNhiems
        //            .Where(p => p.BudgetRegistrationId == id).ToList();
        //        db.BudgetRegistrationPhanNhiems.RemoveRange(phanNhiems);

        //        // 6. Xóa bảng cha
        //        db.BudgetRegistrations.Remove(entity);

        //        db.SaveChanges();

        //        return Json(new
        //        {
        //            success = true,
        //            message = "Xóa hồ sơ thành công."
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine(
        //            $"Delete Error: {ex.Message}");
        //        return Json(new
        //        {
        //            success = false,
        //            message = "Đã xảy ra lỗi khi xóa hồ sơ."
        //        });
        //    }
        //}

        [HttpGet]
        public ActionResult SearchPhanNhiem(int budgetRegistrationId, string search, int? phongBanId, int? chucNangId)
        {
            try
            {
                if (CurrentUser == null)
                    return Json(new
                    {
                        success = false,
                        message = "Phiên đăng nhập đã hết hạn."
                    },
                        JsonRequestBehavior.AllowGet);

                // Kiểm tra quyền xem hồ sơ
                var entity = db.BudgetRegistrations
                    .FirstOrDefault(x => x.BudgetRegistrationId
                                       == budgetRegistrationId);
                if (entity == null)
                    return Json(new
                    {
                        success = false,
                        message = "Không tìm thấy hồ sơ."
                    },
                        JsonRequestBehavior.AllowGet);

                bool isManagerOrAdmin =
                    CurrentUser.RoleId == Constants.RoleConst.Admin ||
                    CurrentUser.RoleId == Constants.RoleConst.Manager;
                bool isOwner = entity.UserId == CurrentUser.UserId;
                bool isPhanNhiem = db.BudgetRegistrationPhanNhiems
                    .Any(p => p.BudgetRegistrationId == budgetRegistrationId
                           && p.UserId == CurrentUser.UserId);

                if (!isManagerOrAdmin && !isOwner && !isPhanNhiem)
                    return Json(new
                    {
                        success = false,
                        message = "Bạn không có quyền xem."
                    },
                        JsonRequestBehavior.AllowGet);

                // Query Phân nhiệm
                var query = db.BudgetRegistrationPhanNhiems
                    .Include(p => p.PhongBan)
                    .Include(p => p.ChucNang_NhiemVu)
                    .Include(p => p.User)
                    .Where(p => p.BudgetRegistrationId == budgetRegistrationId)
                    .AsQueryable();

                // Apply filter tên nhân viên
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(p =>
                        p.User != null &&
                        p.User.HoTen.ToLower().Contains(s));
                }

                // Apply filter Phòng ban
                if (phongBanId.HasValue && phongBanId.Value > 0)
                    query = query.Where(p =>
                        p.PhongBanId == phongBanId.Value);

                // Apply filter Chức năng/Nhiệm vụ
                if (chucNangId.HasValue && chucNangId.Value > 0)
                    query = query.Where(p =>
                        p.ChucNangNhiemVuId == chucNangId.Value);

                var result = query.Select(p => new
                {
                    PhongBanId = p.PhongBanId,
                    TenPhongBan = p.PhongBan.TenPhongBan,
                    ChucNangNhiemVuId = p.ChucNangNhiemVuId,
                    TenChucNang = p.ChucNang_NhiemVu != null
                        ? p.ChucNang_NhiemVu.TenChucNang : null,
                    TenChucNangNhapTay = p.TenChucNangNhapTay,
                    Email = p.Email,
                    GhiChu = p.GhiChu,
                    UserId = p.UserId,
                    TenUser = p.User != null
                        ? p.User.MaNhanVien + " — " + p.User.HoTen : null
                }).ToList();

                return Json(new { success = true, data = result },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SearchPhanNhiem Error: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi."
                },
                    JsonRequestBehavior.AllowGet);
            }
        }

        // SỬA: đổi tham số từ single file sang IEnumerable
        private void SaveHoSoCanCuAndAttachment(
            IEnumerable<HttpPostedFileBase> files,
            int budgetRegistrationId)
        {
            if (files == null) return;

            var uploadFolder = Server.MapPath("~/Uploads/HoSoCanCu");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            foreach (var file in files)
            {
                if (file == null || file.ContentLength <= 0) continue;

                // Validate dung lượng 20MB
                if (file.ContentLength > 20 * 1024 * 1024)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"File {file.FileName} vượt quá 20MB, bỏ qua.");
                    continue;
                }

                var fileExtension = Path.GetExtension(file.FileName);
                var originalFileName = Path.GetFileName(file.FileName);
                var safeFileName = $"{Guid.NewGuid():N}{fileExtension}";
                var fullPath = Path.Combine(uploadFolder, safeFileName);

                try
                {
                    file.SaveAs(fullPath);
                    db.BudgetAttachments.Add(new BudgetAttachment
                    {
                        BudgetRegistrationId = budgetRegistrationId,
                        FileName = originalFileName,
                        FilePath = "/Uploads/HoSoCanCu/" + safeFileName,
                        FileExtension = fileExtension,
                        FileSize = file.ContentLength,
                        UploadedBy = CurrentUser.UserId,
                        UploadedDate = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"SaveHoSoCanCuAndAttachment Error [{file.FileName}]: {ex.Message}");
                }
            }

            db.SaveChanges();
        }

        // ── GET: Budget/GetUsersByPhongBan ───────────────────────────
        [HttpGet]
        public ActionResult GetUsersByPhongBan(int phongBanId)
        {
            try
            {
                var users = db.Users
                    .Where(u => u.PhongBanId == phongBanId
                             && u.TinhTrangLamViec == 1)
                    .OrderBy(u => u.HoTen)
                    .Select(u => new
                    {
                        value = u.UserId,
                        text = u.MaNhanVien + " – " + u.HoTen
                    })
                    .ToList();

                return Json(new { success = true, data = users },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"GetUsersByPhongBan Error: {ex.Message}");
                return Json(new { success = false, data = new object[] { } },
                    JsonRequestBehavior.AllowGet);
            }
        }



        //[HttpPost]
        //public JsonResult TraHoSo(int budgetId)
        //{
        //    if (CurrentUser.RoleId != 1)
        //        return Json(new { success = false, message = "Không có quyền thực hiện." });

        //    var budget = db.BudgetRegistrations.Find(budgetId);
        //    if (budget == null)
        //        return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

        //    if (budget.WorkflowType != null)
        //        return Json(new { success = false, message = "Chỉ có thể trả hồ sơ khi chưa xác nhận luồng." });

        //    budget.WorkflowType = 4;
        //    budget.UpdatedDate = DateTime.Now;
        //    db.SaveChanges();

        //    // THÊM MỚI: gửi thông báo đến chủ hồ sơ
        //    NotificationHelper.Send(
        //        db,
        //        toUserId: budget.UserId,
        //        title: "Hồ sơ bị trả lại",
        //        message: $"Hồ sơ \"{budget.TenHangMuc}\" đã bị trả lại. Vui lòng chỉnh sửa và gửi lại.",
        //        url: $"/Budget/Index?openBudget={budget.BudgetRegistrationId}",
        //        relatedBudgetId: budget.BudgetRegistrationId
        //    );

        //    return Json(new { success = true, message = "Đã trả hồ sơ thành công." });
        //}

        [HttpPost]
        public JsonResult TraHoSo(int budgetId, string lyDoTra)
        {
            if (CurrentUser.RoleId != 1)
                return Json(new
                {
                    success = false,
                    message = "Không có quyền thực hiện."
                });

            if (string.IsNullOrWhiteSpace(lyDoTra))
                return Json(new
                {
                    success = false,
                    message = "Vui lòng nhập lý do trả hồ sơ."
                });

            var budget = db.BudgetRegistrations.Find(budgetId);
            if (budget == null)
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy hồ sơ."
                });

            if (budget.WorkflowType != null)
                return Json(new
                {
                    success = false,
                    message = "Chỉ có thể trả hồ sơ khi chưa xác nhận luồng."
                });

            budget.WorkflowType = 4;
            budget.LyDoTra = lyDoTra.Trim(); // THÊM MỚI
            budget.UpdatedDate = DateTime.Now;
            db.SaveChanges();

            // Gửi thông báo kèm lý do đến chủ hồ sơ
            NotificationHelper.Send(
                db,
                toUserId: budget.UserId,
                title: "Hồ sơ bị trả lại",
                message: $"Hồ sơ \"{budget.TenHangMuc}\" đã bị trả lại. Lý do: {lyDoTra.Trim()}",
                url: $"/Budget/Index?openBudget={budget.BudgetRegistrationId}",
                relatedBudgetId: budget.BudgetRegistrationId
            );

            return Json(new
            {
                success = true,
                message = "Đã trả hồ sơ thành công."
            });
        }

        // ================================================================
        // POST: /Budget/SubmitRevision
        // User gửi lại hồ sơ sau khi chỉnh sửa
        // ================================================================
        [HttpPost]
        public JsonResult SubmitRevision(SubmitRevisionDto dto)
        {
            if (dto == null || dto.BudgetRegistrationId <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var budget = db.BudgetRegistrations.Find(dto.BudgetRegistrationId);
            if (budget == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            // Chỉ cho phép gửi lại khi hồ sơ đang ở trạng thái Trả hồ sơ (4)
            if (budget.WorkflowType != 4)
                return Json(new { success = false, message = "Hồ sơ không ở trạng thái cho phép chỉnh sửa." });

            var now = DateTime.Now;

            // Tìm revision đang chờ duyệt (nếu có) → cập nhật đè
            var revision = db.BudgetRevisions
                             .FirstOrDefault(r => r.BudgetRegistrationId == dto.BudgetRegistrationId
                                               && r.RevisionStatus == 1);
            if (revision == null)
            {
                revision = new BudgetRevision
                {
                    BudgetRegistrationId = dto.BudgetRegistrationId,
                    CreatedBy = CurrentUser.UserId,
                    CreatedDate = now
                };
                db.BudgetRevisions.Add(revision);
            }

            // ── LOGIC XỬ LÝ CHUẨN HÓA LÝ DO ĐẦU TƯ KHI USER GỬI LẠI HỒ SƠ ──
            bool isManualReason = false;
            if (dto.InvestmentReasonId.HasValue)
            {
                var reasonCategory = db.BudgetInvestmentReasons.Find(dto.InvestmentReasonId.Value);
                if (reasonCategory != null && reasonCategory.IsManual)
                {
                    isManualReason = true;
                }
            }

            if (isManualReason)
            {
                // Nếu chọn Điền thủ công: Lưu chuỗi nhập tay, set ID danh mục bằng NULL
                revision.InvestmentReasonId = null;
                revision.LyDoDauTu = dto.LyDoDauTu?.Trim();
            }
            else
            {
                // Nếu chọn danh mục mẫu: Lưu khóa ngoại, clear trắng trường text nhập tay
                revision.InvestmentReasonId = dto.InvestmentReasonId;
                revision.LyDoDauTu = null;
            }


            revision.ProjectAreaId = dto.ProjectAreaId;
            revision.TenHangMuc = dto.TenHangMuc;
            revision.DuToan = dto.DuToan;
            revision.SoToTrinh = dto.SoToTrinh;
            revision.CategoryTypeId = dto.CategoryTypeId;
            revision.PriorityLevelId = dto.PriorityLevelId;
            revision.SoLuong = dto.SoLuong;
            revision.MoTaKyThuat = dto.MoTaKyThuat;
            revision.NgayBatDau = string.IsNullOrEmpty(dto.NgayBatDau)
                                             ? (DateTime?)null
                                             : DateTime.Parse(dto.NgayBatDau);
            revision.NgayKetThuc = string.IsNullOrEmpty(dto.NgayKetThuc)
                                             ? (DateTime?)null
                                             : DateTime.Parse(dto.NgayKetThuc);
            revision.LinkTaiLieuLienQuan = dto.LinkTaiLieuLienQuan;
            revision.RevisionStatus = 1; // Chờ duyệt
            revision.CreatedDate = now;

            db.SaveChanges();

            var revFiles = Request.Files.GetMultiple("HoSoCanCu")
                            .Where(f => f != null && f.ContentLength > 0)
                            .ToList();

            if (revFiles.Any())
            {
                // Xóa file revision cũ (nếu đang đè lên revision cũ)
                var oldRevAttachments = db.BudgetRevisionAttachments
                                          .Where(a => a.RevisionId == revision.RevisionId)
                                          .ToList();
                foreach (var old in oldRevAttachments)
                {
                    try
                    {
                        var physicalPath = Server.MapPath("~" + old.FilePath);
                        if (System.IO.File.Exists(physicalPath))
                            System.IO.File.Delete(physicalPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"SubmitRevision - Xóa RevisionAttachment lỗi [{old.FilePath}]: {ex.Message}");
                    }
                }
                db.BudgetRevisionAttachments.RemoveRange(oldRevAttachments);
                db.SaveChanges();

                // Lưu file mới vào BudgetRevisionAttachments
                // KHÔNG xóa BudgetAttachments — chờ Admin duyệt mới xóa
                foreach (var revFile in revFiles)
                    _SaveRevisionAttachment(revFile, revision.RevisionId);
            }

            // Gửi thông báo đến tất cả Admin
            var adminUsers = db.Users
                               .Where(u => u.RoleId == 1)
                               .ToList();

            foreach (var admin in adminUsers)
            {
                NotificationHelper.Send(
                    db,
                    toUserId: admin.UserId,
                    title: "Hồ sơ chờ duyệt thay đổi",
                    message: $"{CurrentUser.TenPhongBan} đã gửi lại hồ sơ \"{budget.TenHangMuc}\". Vui lòng xem xét và duyệt.",
                    url: $"/Budget/Revisions?openRevision={revision.RevisionId}",
                    relatedRevisionId: revision.RevisionId
                );
            }

            return Json(new { success = true, message = "Gửi lại hồ sơ thành công. Vui lòng chờ duyệt." });
        }

        // ================================================================
        // POST: /Budget/ApproveRevision
        // Admin duyệt thay đổi → ghi đè BudgetRegistrations
        // ================================================================
        [HttpPost]
        public JsonResult ApproveRevision(int revisionId)
        {
            if (CurrentUser.RoleId != 1)
                return Json(new { success = false, message = "Không có quyền thực hiện." });

            var revision = db.BudgetRevisions.Find(revisionId);
            if (revision == null)
                return Json(new { success = false, message = "Không tìm thấy revision." });

            var budget = db.BudgetRegistrations.Find(revision.BudgetRegistrationId);
            if (budget == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            // Ghi đè BudgetRegistrations
            budget.ProjectAreaId = revision.ProjectAreaId ?? budget.ProjectAreaId;
            budget.TenHangMuc = revision.TenHangMuc ?? budget.TenHangMuc;
            budget.DuToan = revision.DuToan ?? budget.DuToan;
            budget.SoToTrinh = revision.SoToTrinh ?? budget.SoToTrinh;
            budget.CategoryTypeId = revision.CategoryTypeId ?? budget.CategoryTypeId;
            budget.PriorityLevelId = revision.PriorityLevelId ?? budget.PriorityLevelId;
            budget.SoLuong = revision.SoLuong ?? budget.SoLuong;
            budget.InvestmentReasonId = revision.InvestmentReasonId;
            budget.LyDoDauTu = revision.LyDoDauTu ?? budget.LyDoDauTu;
            budget.MoTaKyThuat = revision.MoTaKyThuat ?? budget.MoTaKyThuat;
            budget.NgayBatDau = revision.NgayBatDau ?? budget.NgayBatDau;
            budget.NgayKetThuc = revision.NgayKetThuc ?? budget.NgayKetThuc;
            budget.LinkTaiLieuLienQuan = revision.LinkTaiLieuLienQuan ?? budget.LinkTaiLieuLienQuan;
            // Chỉ reset WorkflowType nếu là RevisionType = 1 (trả hồ sơ)
            if (revision.RevisionType == 1)
            {
                budget.WorkflowType = null;
            }
            budget.UpdatedDate = DateTime.Now;

            // Đánh dấu revision đã duyệt
            revision.RevisionStatus = 2;
            revision.ReviewedDate = DateTime.Now;
            revision.ReviewedBy = CurrentUser.UserId;

            db.SaveChanges();

            var revisionAttachments = db.BudgetRevisionAttachments
                                .Where(a => a.RevisionId == revisionId)
                                .ToList();

            // Chỉ xóa file cũ và thay thế khi revision có file mới
            if (revisionAttachments.Any())
            {
                // Bước 1: Lấy toàn bộ file cũ của hồ sơ
                var oldAttachments = db.BudgetAttachments
                                       .Where(a => a.BudgetRegistrationId
                                                == revision.BudgetRegistrationId)
                                       .ToList();

                // Bước 2: Xóa file vật lý trên server
                foreach (var old in oldAttachments)
                {
                    try
                    {
                        var physicalPath = Server.MapPath("~" + old.FilePath);
                        if (System.IO.File.Exists(physicalPath))
                            System.IO.File.Delete(physicalPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ApproveRevision - Xóa file lỗi [{old.FilePath}]: {ex.Message}");
                    }
                }

                // Bước 3: Xóa bản ghi file cũ trong DB
                db.BudgetAttachments.RemoveRange(oldAttachments);

                // Bước 4: Copy file mới từ BudgetRevisionAttachments → BudgetAttachments
                foreach (var ra in revisionAttachments)
                {
                    db.BudgetAttachments.Add(new BudgetAttachment
                    {
                        BudgetRegistrationId = revision.BudgetRegistrationId,
                        FileName = ra.FileName,
                        FilePath = ra.FilePath,
                        FileExtension = ra.FileExtension,
                        FileSize = (long) ra.FileSize,
                        UploadedBy = ra.UploadedBy,
                        UploadedDate = ra.UploadedDate
                    });
                }

                db.SaveChanges();
            }

            // THÊM MỚI: gửi thông báo đến chủ hồ sơ
            // Gửi thông báo đến chủ hồ sơ
            NotificationHelper.Send(
                db,
                toUserId: budget.UserId,
                title: revision.RevisionType == 1
                                    ? "Hồ sơ đã được duyệt"
                                    : "Thông tin chỉnh sửa đã được xác nhận",
                message: revision.RevisionType == 1
                                    ? $"Hồ sơ \"{budget.TenHangMuc}\" đã được duyệt thay đổi thành công."
                                    : $"Thông tin chỉnh sửa hồ sơ \"{budget.TenHangMuc}\" đã được xác nhận.",
                url: $"/Budget/Index?openBudget={budget.BudgetRegistrationId}",
                relatedBudgetId: budget.BudgetRegistrationId
            );

            return Json(new
            {
                success = true,
                message = "Đã duyệt thay đổi. Vui lòng xác nhận lại luồng xử lý cho hồ sơ này."
            });
        }

        // ================================================================
        // GET: /Budget/Revisions
        // Trang Admin xem danh sách hồ sơ có Revision chờ duyệt
        // ================================================================
        [HttpGet]
        public ActionResult Revisions()
        {
            if (CurrentUser.RoleId != 1)
                return RedirectToAction("Index");

            var list = db.BudgetRevisions
             .Where(r => r.RevisionStatus == 1)
             .OrderByDescending(r => r.CreatedDate)
             .Select(r => new RevisionListItemViewModel
             {
                 RevisionId = r.RevisionId,
                 BudgetRegistrationId = r.BudgetRegistrationId,
                 MaHangMuc = r.BudgetRegistration.MaHangMuc,
                 TenHangMuc = r.TenHangMuc,
                 CreatedDate = r.CreatedDate,

                 CreatedByName = r.BudgetRegistration.PhongBan != null
                                 ? r.BudgetRegistration.PhongBan.TenPhongBan
                                 : "N/A",
                 RevisionType = r.RevisionType  // THÊM MỚI
             })
             .ToList();

            return View(list);
        }

        // ================================================================
        // GET: /Budget/GetRevisionDetail?revisionId=xxx
        // Load chi tiết revision cho modal trên trang Revisions
        // ================================================================
        [HttpGet]
        public JsonResult GetRevisionDetail(int revisionId)
        {
            if (CurrentUser.RoleId != 1)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var r = db.BudgetRevisions.Find(revisionId);
            if (r == null)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            // ── XỬ LÝ LẤY CHUỖI TEXT LÝ DO ĐẦU TƯ ĐỂ HIỂN THỊ DẠNG TEXT THUẦN ──
            string hienThiLyDoDauTu = string.Empty;
            if (r.InvestmentReasonId.HasValue)
            {
                // Nếu chọn lý do mẫu: Lấy ReasonName từ DB danh mục
                hienThiLyDoDauTu = db.BudgetInvestmentReasons
                                     .Where(x => x.InvestmentReasonId == r.InvestmentReasonId.Value)
                                     .Select(x => x.ReasonName)
                                     .FirstOrDefault() ?? string.Empty;
            }
            else
            {
                // Nếu là điền thủ công hoặc dữ liệu cũ: Lấy chuỗi nhập tay từ trường LyDoDauTu
                hienThiLyDoDauTu = r.LyDoDauTu ?? string.Empty;
            }

            // Lấy tên Khu vực, Loại hạng mục, Mức ưu tiên
            var tenKhuVuc = r.ProjectAreaId.HasValue
                ? db.ProjectAreas
                    .Where(a => a.ProjectAreaId == r.ProjectAreaId.Value)
                    .Select(a => a.AreaName)
                    .FirstOrDefault()
                : null;

            var tenCategoryType = r.CategoryTypeId.HasValue
                ? db.BudgetCategoryTypes
                    .Where(c => c.CategoryTypeId == r.CategoryTypeId.Value)
                    .Select(c => c.CategoryTypeName)
                    .FirstOrDefault()
                : null;

            var tenPriorityLevel = r.PriorityLevelId.HasValue
                ? db.BudgetPriorityLevels
                    .Where(p => p.PriorityLevelId == r.PriorityLevelId.Value)
                    .Select(p => p.PriorityLevelName)
                    .FirstOrDefault()
                : null;

            var revisionAttachments = db.BudgetRevisionAttachments
                                .Where(a => a.RevisionId == revisionId)
                                .Select(a => new
                                {
                                    a.AttachmentId,
                                    a.FileName,
                                    a.FilePath,
                                    a.FileExtension,
                                    FileSize = (long?)a.FileSize
                                }).ToList();

            var attachments = revisionAttachments.Any()
                ? revisionAttachments
                : db.BudgetAttachments
                    .Where(a => a.BudgetRegistrationId == r.BudgetRegistrationId)
                    .Select(a => new
                    {
                        a.AttachmentId,
                        a.FileName,
                        a.FilePath,
                        a.FileExtension,
                        FileSize = (long?)a.FileSize
                    }).ToList();

            return Json(new
            {
                success = true,
                r.RevisionId,
                r.BudgetRegistrationId,
                r.ProjectAreaId,
                TenKhuVuc = tenKhuVuc,        // THÊM MỚI
                r.TenHangMuc,
                r.DuToan,
                r.SoToTrinh,
                r.CategoryTypeId,
                TenCategoryType = tenCategoryType,  // THÊM MỚI
                r.PriorityLevelId,
                TenPriorityLevel = tenPriorityLevel, // THÊM MỚI
                r.SoLuong,
                LyDoDauTu = hienThiLyDoDauTu,
                r.MoTaKyThuat,
                NgayBatDau = r.NgayBatDau.HasValue
                                ? r.NgayBatDau.Value.ToString("yyyy-MM-dd") : null,
                NgayKetThuc = r.NgayKetThuc.HasValue
                                ? r.NgayKetThuc.Value.ToString("yyyy-MM-dd") : null,
                r.LinkTaiLieuLienQuan,
                Attachments = attachments,
                r.RevisionType,    // THÊM MỚI
                r.LyDoChinhSua     // THÊM MỚI
            }, JsonRequestBehavior.AllowGet);
        }

        // ================================================================
        // GET: /Budget/GetNotifications
        // ================================================================
        //[HttpGet]
        //public JsonResult GetNotifications()
        //{
        //    var list = db.Notifications
        //                 .Where(n => n.UserId == CurrentUser.UserId)
        //                 .OrderByDescending(n => n.CreatedDate)
        //                 .Take(20)
        //                 .Select(n => new
        //                 {
        //                     n.NotificationId,
        //                     n.Title,
        //                     n.Message,
        //                     n.Url,
        //                     n.IsRead,
        //                     CreatedDate = n.CreatedDate
        //                 })
        //                 .ToList()
        //                 .Select(n => new
        //                 {
        //                     n.NotificationId,
        //                     n.Title,
        //                     n.Message,
        //                     n.Url,
        //                     n.IsRead,
        //                     CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy HH:mm")
        //                 })
        //                 .ToList();

        //    var unreadCount = db.Notifications
        //                        .Count(n => n.UserId == CurrentUser.UserId
        //                                 && !n.IsRead);

        //    return Json(new { list, unreadCount },
        //                JsonRequestBehavior.AllowGet);
        //}

        // ================================================================
        // GET: /Budget/GetNotifications
        // ================================================================
        [HttpGet]
        public ContentResult GetNotifications() // ĐỔI từ JsonResult thành ContentResult để kiểm soát JSON format
        {
            var list = db.Notifications
                         .Where(n => n.UserId == CurrentUser.UserId)
                         .OrderByDescending(n => n.CreatedDate)
                         .Take(20)
                         .Select(n => new
                         {
                             n.NotificationId,
                             n.Title,
                             n.Message,
                             n.Url,
                             n.IsRead,
                             CreatedDate = n.CreatedDate
                         })
                         .ToList()
                         .Select(n => new
                         {
                             n.NotificationId,
                             n.Title,
                             n.Message,
                             n.Url,
                             n.IsRead,
                             CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy HH:mm")
                         })
                         .ToList();

            var unreadCount = db.Notifications
                                .Count(n => n.UserId == CurrentUser.UserId
                                         && !n.IsRead);

            // Cấu hình ép JSON trả về dạng camelCase (chữ cái đầu viết thường)
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            string jsonResult = JsonConvert.SerializeObject(new { list, unreadCount }, settings);

            return Content(jsonResult, "application/json");
        }

        // ================================================================
        // POST: /Budget/MarkNotificationRead
        // ================================================================
        [HttpPost]
        public JsonResult MarkNotificationRead(int notificationId)
        {
            var noti = db.Notifications
                         .FirstOrDefault(n => n.NotificationId == notificationId
                                           && n.UserId == CurrentUser.UserId);
            if (noti != null)
            {
                noti.IsRead = true;
                db.SaveChanges();
            }
            return Json(new { success = true });
        }

        // ================================================================
        // POST: /Budget/MarkAllNotificationsRead
        // ================================================================
        [HttpPost]
        public JsonResult MarkAllNotificationsRead()
        {
            var unread = db.Notifications
                           .Where(n => n.UserId == CurrentUser.UserId
                                     && !n.IsRead)
                           .ToList();
            unread.ForEach(n => n.IsRead = true);
            db.SaveChanges();
            return Json(new { success = true });
        }

        // ================================================================
        // Private: lưu file đính kèm cho Revision
        // ================================================================
        private void _SaveRevisionAttachment(HttpPostedFileBase file, int revisionId)
        {
            var uploadFolder = Server.MapPath("~/Uploads/HoSoCanCu");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var fileExtension = Path.GetExtension(file.FileName);
            var originalFileName = Path.GetFileName(file.FileName);
            var safeFileName = $"{Guid.NewGuid():N}{fileExtension}";
            var fullPath = Path.Combine(uploadFolder, safeFileName);

            try
            {
                file.SaveAs(fullPath);
                db.BudgetRevisionAttachments.Add(new BudgetRevisionAttachment
                {
                    RevisionId = revisionId,
                    FileName = originalFileName,
                    FilePath = "/Uploads/HoSoCanCu/" + safeFileName,
                    FileExtension = fileExtension,
                    FileSize = file.ContentLength,
                    UploadedBy = CurrentUser.UserId,
                    UploadedDate = DateTime.Now
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"_SaveRevisionAttachment Error: {ex.Message}");
            }
        }

        private void ReloadDropdowns(CreateBudgetRegistrationViewModel model)
        {
            model.CategoryTypes = GetCategoryTypes();
            model.PriorityLevels = GetPriorityLevels();
            model.DanhSachPhongBan = GetPhongBanSelectList();
            model.DanhSachChucNang = GetChucNangSelectList();
            model.DanhSachKhuVuc = GetProjectAreaSelectList();
        }

        private IEnumerable<SelectListItem> GetCategoryTypes()
        {
            return db.BudgetCategoryTypes
                .Where(x => x.IsActive)
                .OrderBy(x => x.CategoryTypeId)
                .Select(x => new SelectListItem
                {
                    Value = x.CategoryTypeId.ToString(),
                    Text = x.CategoryTypeName
                })
                .ToList();
        }

        private IEnumerable<SelectListItem> GetPriorityLevels()
        {
            return db.BudgetPriorityLevels
                .Where(x => x.IsActive)
                .OrderBy(x => x.PriorityLevelId)
                .Select(x => new SelectListItem
                {
                    Value = x.PriorityLevelId.ToString(),
                    Text = x.PriorityLevelName
                })
                .ToList();
        }

        private IEnumerable<SelectListItem> GetInvestmentReasonSelectList()
        {
            return db.BudgetInvestmentReasons
                .OrderBy(x => x.InvestmentReasonId)
                .Select(x => new SelectListItem
                {
                    Value = x.InvestmentReasonId.ToString(),
                    Text = x.ReasonName
                })
                .ToList();
        }

        /// <summary>
        /// Tạo mã hạng mục theo format: MaPB.LoaiHangMuc.MMYY.Sequence
        /// VD: PIT.01.0126.01
        /// </summary>
        /// <param name="loaiHangMucId">ID loại hạng mục (mặc định là 1)</param>
        /// <returns>Mã hạng mục được tạo tự động</returns>
        private string GenerateMaHangMuc(int loaiHangMucId = 1)
        {
            try
            {
                // Validate current user
                if (CurrentUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("GenerateMaHangMuc: CurrentUser is null");
                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(CurrentUser.MaPhongBan))
                {
                    System.Diagnostics.Debug.WriteLine("GenerateMaHangMuc: MaPhongBan is null or empty");
                    return string.Empty;
                }

                if (CurrentUser.PhongBanId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("GenerateMaHangMuc: Invalid PhongBanId");
                    return string.Empty;
                }

                // Validate loaiHangMucId
                if (loaiHangMucId <= 0)
                {
                    loaiHangMucId = 1; // Default value
                }

                // Get current month and year (MMYY format)
                string currentMonthYear = DateTime.Now.ToString("MMyy");

                // Calculate sequence for this department, category and month
                int sequence = CalculateSequence(CurrentUser.PhongBanId, loaiHangMucId, currentMonthYear);

                // Format components
                string maPB = CurrentUser.MaPhongBan.Trim().ToUpper();
                string loaiHM = loaiHangMucId.ToString("D2"); // Format: 01, 02, 03...
                string sequenceFormatted = sequence.ToString("D2"); // Format: 01, 02, 03...

                // Generate final code: PIT.01.0126.01
                string maHangMuc = $"{maPB}.{loaiHM}.{currentMonthYear}.{sequenceFormatted}";

                return maHangMuc;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateMaHangMuc Error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Tính toán sequence number cho mã hạng mục
        /// Sequence được reset mỗi tháng cho mỗi phòng ban và loại hạng mục
        /// </summary>
        /// <param name="phongBanId">ID phòng ban</param>
        /// <param name="loaiHangMucId">ID loại hạng mục</param>
        /// <param name="monthYear">Tháng năm theo format MMYY</param>
        /// <returns>Sequence number tiếp theo</returns>
        private int CalculateSequence(int phongBanId, int loaiHangMucId, string monthYear)
        {
            try
            {
                // Get current month and year boundaries
                int currentMonth = DateTime.Now.Month;
                int currentYear = DateTime.Now.Year;

                DateTime startOfMonth = new DateTime(currentYear, currentMonth, 1);
                DateTime endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

                // Count existing records for this department, category, and month
                int count = db.BudgetRegistrations
                    .Where(x => x.PhongBanId == phongBanId
                             //&& x.CategoryTypeId == loaiHangMucId
                             && x.CreatedDate >= startOfMonth && x.CreatedDate <= endOfMonth
                            )
                    .Count();

                // Return next sequence number
                return count + 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateSequence Error: {ex.Message}");
                // Fallback to 1 if error occurs
                return 1;
            }
        }

        /// <summary>
        /// AJAX endpoint để tạo mã hạng mục mới khi thay đổi loại hạng mục
        /// </summary>
        [HttpPost]
        public JsonResult GenerateNewMaHangMuc(int loaiHangMucId)
        {
            try
            {
                if (CurrentUser == null)
                {
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
                }

                if (loaiHangMucId <= 0)
                {
                    return Json(new { success = false, message = "Loại hạng mục không hợp lệ." });
                }

                string maHangMuc = GenerateMaHangMuc(loaiHangMucId);

                if (string.IsNullOrWhiteSpace(maHangMuc))
                {
                    return Json(new { success = false, message = "Không thể tạo mã hạng mục." });
                }

                return Json(new { success = true, maHangMuc = maHangMuc });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateNewMaHangMuc Error: {ex.Message}");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi tạo mã hạng mục." });
            }
        }

        /// <summary>
        /// Kiểm tra hồ sơ có đầy đủ thông tin không
        /// Đầy đủ nếu có: MaHangMuc, TenHangMuc, DuToan, SoToTrinh, LyDoDauTu, MoTaKyThuat
        /// </summary>
        /// <param name="model">ViewModel của hồ sơ</param>
        /// <returns>True nếu đầy đủ, False nếu thiếu</returns>
        private bool IsCompleteInformation(CreateBudgetRegistrationViewModel model)
        {
            try
            {
                // Kiểm tra các trường bắt buộc
                //bool isComplete = !string.IsNullOrWhiteSpace(model.MaHangMuc) &&
                //                 !string.IsNullOrWhiteSpace(model.TenHangMuc) &&
                //                 model.DuToan > 0 &&
                //                 !string.IsNullOrWhiteSpace(model.SoToTrinh) &&
                //                 !string.IsNullOrWhiteSpace(model.LyDoDauTu) &&
                //                 !string.IsNullOrWhiteSpace(model.MoTaKyThuat);

                // Kiểm tra các trường bắt buộc
                bool isComplete = !string.IsNullOrWhiteSpace(model.LinkTaiLieuLienQuan);

                return isComplete;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsCompleteInformation Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xác định trạng thái hồ sơ dựa trên thông tin đầy đủ
        /// </summary>
        /// <param name="model">ViewModel của hồ sơ</param>
        /// <returns>TrangThaiHoSo enum value</returns>
        private int DetermineHoSoStatus(CreateBudgetRegistrationViewModel model)
        {
            return IsCompleteInformation(model) ? (int)TrangThaiHoSo.DuThongTin : (int)TrangThaiHoSo.ThieuThongTin;
        }

        /// <summary>
        /// GET: Budget/Details/5 - Xem chi tiết đăng ký ngân sách
        /// </summary>
        /// <param name="id">ID của đăng ký ngân sách</param>
        /// <returns>View hiển thị chi tiết đăng ký</returns>
        public ActionResult Details(int? id)
        {
            try
            {
                // Validate id parameter
                if (!id.HasValue || id.Value <= 0)
                {
                    TempData["Error"] = "Mã đăng ký không hợp lệ.";
                    return RedirectToAction("Index");
                }

                // Validate current user
                if (CurrentUser == null)
                {
                    TempData["Error"] = "Phiên đăng nhập đã hết hạn.";
                    return RedirectToAction("Login", "Account");
                }

                // Query budget registration with all related data using eager loading
                var budgetRegistration = db.BudgetRegistrations
                    .Include(x => x.PhongBan)
                    .Include(x => x.User)
                    .Include(x => x.BudgetCategoryType)
                    .Include(x => x.BudgetPriorityLevel)
                    .Include(x => x.BudgetAttachments.Select(a => a.User))
                    .FirstOrDefault(x => x.BudgetRegistrationId == id.Value);

                // Check if record exists
                if (budgetRegistration == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin đăng ký.";
                    return RedirectToAction("Index");
                }

                // Check permissions
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;

                // User thường chỉ được xem đăng ký của mình

                if (!isManagerOrAdmin && budgetRegistration.UserId != CurrentUser.UserId)
                {
                    TempData["Error"] = "Bạn không có quyền xem đăng ký này.";
                    return RedirectToAction("Index");
                }

                // Map to view model
                var viewModel = new BudgetRegistrationDetailsViewModel
                {
                    // Basic Information
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    TenPhongBan = budgetRegistration.PhongBan.TenPhongBan,
                    MaHangMuc = budgetRegistration.MaHangMuc ?? string.Empty,
                    TenHangMuc = budgetRegistration.TenHangMuc ?? string.Empty,
                    DuToan = (decimal) budgetRegistration.DuToan,
                    SoToTrinh = budgetRegistration.SoToTrinh ?? string.Empty,
                    SoLuong = budgetRegistration.SoLuong,
                    LyDoDauTu = budgetRegistration.LyDoDauTu ?? string.Empty,
                    MoTaKyThuat = budgetRegistration.MoTaKyThuat,
                    LinkTaiLieuLienQuan = budgetRegistration.LinkTaiLieuLienQuan,

                    // Category & Priority
                    //CategoryTypeId = budgetRegistration.CategoryTypeId,
                    CategoryTypeName = budgetRegistration.BudgetCategoryType?.CategoryTypeName ?? "N/A",
                    //PriorityLevelId = budgetRegistration.PriorityLevelId,
                    PriorityLevelName = budgetRegistration.BudgetPriorityLevel?.PriorityLevelName ?? "N/A",

                    // Dates
                    NgayBatDau = budgetRegistration.NgayBatDau,
                    NgayKetThuc = budgetRegistration.NgayKetThuc,
                    NgayTao = budgetRegistration.CreatedDate,
                    //NgayCapNhat = budgetRegistration.UpdatedDate,

                    // Department Information
                    //PhongBanId = budgetRegistration.PhongBanId,
                    //TenPhongBan = budgetRegistration.PhongBan?.TenPhongBan ?? "N/A",
                    //MaPhongBan = budgetRegistration.PhongBan?.MaPhongBan ?? "N/A",

                    // User Information
                    //UserId = budgetRegistration.UserId,
                    //NguoiDangKy = budgetRegistration.User?.HoTen ?? "N/A",
                    //EmailNguoiDangKy = budgetRegistration.User?.Email,
                    //MaNhanVienDangKy = budgetRegistration.User?.MaNhanVien,

                    // Status (nếu có field TrangThai)
                    // TrangThai = budgetRegistration.TrangThai ?? "Chờ duyệt",

                    // Attachments
                    Attachments = budgetRegistration.BudgetAttachments
                        .OrderByDescending(a => a.UploadedDate)
                        .Select(a => new BudgetAttachmentViewModel
                        {
                            AttachmentId = a.AttachmentId,
                            FileName = a.FileName ?? string.Empty,
                            FilePath = a.FilePath ?? string.Empty,
                            FileExtension = a.FileExtension ?? string.Empty,
                            FileSize = a.FileSize,
                            FileSizeFormatted = FormatFileSize(a.FileSize),
                            UploadedBy = a.User?.HoTen ?? "N/A",
                            UploadedDate = a.UploadedDate
                        })
                        .ToList(),

                    // Permissions
                    //IsManagerOrAdmin = isManagerOrAdmin,
                    //CanEdit = budgetRegistration.UserId == CurrentUser.UserId,
                    //CanDelete = budgetRegistration.UserId == CurrentUser.UserId,
                    //IsOwner = budgetRegistration.UserId == CurrentUser.UserId
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log error với đầy đủ thông tin
                System.Diagnostics.Debug.WriteLine($"Details Error - ID: {id}, Message: {ex.Message}, StackTrace: {ex.StackTrace}");

                TempData["Error"] = "Đã xảy ra lỗi khi tải thông tin chi tiết. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// GET: Budget/GetDetailsModal/5 - Lấy thông tin chi tiết để hiển thị trong modal
        /// </summary>
        [HttpGet]
        public ActionResult GetDetailsModal_OLD(int? id)
        {
            try
            {
                // Validate id parameter
                if (!id.HasValue || id.Value <= 0)
                {
                    return Json(new { success = false, message = "Mã đăng ký không hợp lệ." }, JsonRequestBehavior.AllowGet);
                }

                // Validate current user
                if (CurrentUser == null)
                {
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." }, JsonRequestBehavior.AllowGet);
                }

                // Query budget registration with all related data
                var budgetRegistration = db.BudgetRegistrations
                .Include(x => x.PhongBan)
                .Include(x => x.User)
                .Include(x => x.BudgetCategoryType)
                .Include(x => x.BudgetPriorityLevel)
                .Include(x => x.BudgetAttachments.Select(a => a.User))
                .Include(x => x.ProjectArea)
                .Include(x => x.BudgetRegistrationPhanNhiems
                               .Select(p => p.PhongBan))
                .Include(x => x.BudgetRegistrationPhanNhiems
                               .Select(p => p.ChucNang_NhiemVu))
                .Include(x => x.BudgetApprovals)
                .Include(x => x.ProgressConfigs)
                //.Include(x => x.ProgressAreas.Select(a => a.ProgressAreaItems))
                .Include(x => x.BudgetRegistrationPhanNhiems.Select(p => p.User))
                .FirstOrDefault(x => x.BudgetRegistrationId == id.Value);

                // Check if record exists
                if (budgetRegistration == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin đăng ký." }, JsonRequestBehavior.AllowGet);
                }

                // 1. TÍNH TOÁN CÁC BIẾN PHỤ TRỢ CHO TRẠNG THÁI HIỂN THỊ
                var approvals = budgetRegistration.BudgetApprovals.ToList();
                var approvalGoc = approvals.FirstOrDefault(x => !x.IsSupplementary);

                string soToTrinhRaw = budgetRegistration.SoToTrinh;
                int? workflowType = budgetRegistration.WorkflowType;
                int trangThaiPheDuyetGoc = approvalGoc?.TrangThaiPheDuyet ?? 0;
                bool coBoSungChuaDuyet = approvals.Any(a => a.IsSupplementary && a.TrangThaiPheDuyet != 2);

                // 2. LOGIC XÁC ĐỊNH TRANG THÁI HIỂN THỊ
                int trangThaiHienThi = 1; // Mặc định: Đăng ký mới

                if (string.IsNullOrEmpty(soToTrinhRaw))
                {
                    trangThaiHienThi = 0; // Chưa có chủ trương
                }
                else if (workflowType == null)
                {
                    trangThaiHienThi = 1; // Đăng ký mới
                }
                else if (workflowType == 2)
                {
                    trangThaiHienThi = 5; // Luồng chi phí sản xuất
                }
                else if (workflowType == 3)
                {
                    trangThaiHienThi = 6; // Chưa đủ hồ sơ
                }
                else if (trangThaiPheDuyetGoc == 2 && coBoSungChuaDuyet)
                {
                    trangThaiHienThi = 4; // Đang bổ sung ngân sách
                }
                else if (trangThaiPheDuyetGoc == 2 && !coBoSungChuaDuyet)
                {
                    trangThaiHienThi = 3; // Đã phê duyệt ngân sách
                }
                else if (workflowType != null)
                {
                    trangThaiHienThi = 2; // Đang thực hiện xin ngân sách
                }

                // Check permissions
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;
                
                bool isPhanNhiemUser =
                    !isManagerOrAdmin
                    && budgetRegistration.UserId != CurrentUser.UserId
                    && budgetRegistration.BudgetRegistrationPhanNhiems
                        .Any(p => p.UserId == CurrentUser.UserId);

                if (!isManagerOrAdmin && budgetRegistration.UserId != CurrentUser.UserId && !isPhanNhiemUser)
                {
                    return Json(new { success = false, message = "Bạn không có quyền xem đăng ký này." }, JsonRequestBehavior.AllowGet);
                }
                
                // Map to view model
                var viewModel = new BudgetRegistrationDetailsViewModel
                {
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    MaHangMuc = budgetRegistration.MaHangMuc ?? string.Empty,
                    TenHangMuc = budgetRegistration.TenHangMuc ?? string.Empty,
                    DuToan = budgetRegistration.DuToan ?? 0,
                    SoToTrinh = budgetRegistration.SoToTrinh ?? string.Empty,
                    SoLuong = budgetRegistration.SoLuong,
                    LyDoDauTu = budgetRegistration.LyDoDauTu ?? string.Empty,
                    MoTaKyThuat = budgetRegistration.MoTaKyThuat,
                    LinkTaiLieuLienQuan = budgetRegistration.LinkTaiLieuLienQuan,

                    CategoryTypeName = budgetRegistration.BudgetCategoryType?.CategoryTypeName ?? "N/A",
                    PriorityLevelName = budgetRegistration.BudgetPriorityLevel?.PriorityLevelName ?? "N/A",

                    NgayBatDau = budgetRegistration.NgayBatDau,
                    NgayKetThuc = budgetRegistration.NgayKetThuc,
                    NgayTao = budgetRegistration.CreatedDate,

                    TenPhongBan = budgetRegistration.PhongBan.TenPhongBan,

                    UserId = budgetRegistration.UserId,
                    TrangThaiHienThi = trangThaiHienThi,
                    TenNguoiDangKy = budgetRegistration.User.HoTen,

                    WorkflowType = budgetRegistration.WorkflowType,

                    IsManagerOrAdmin = isManagerOrAdmin,

                    ProjectAreaId = budgetRegistration.ProjectAreaId,

                    // Phân nhiệm
                    DanhSachPhanNhiem = budgetRegistration.BudgetRegistrationPhanNhiems
                    .Select(p => new PhanNhiemViewModel
                    {
                        PhongBanId = p.PhongBanId,
                        TenPhongBan = p.PhongBan?.TenPhongBan,
                        ChucNangNhiemVuId = p.ChucNangNhiemVuId,
                        TenChucNang = p.ChucNang_NhiemVu?.TenChucNang,
                        TenChucNangNhapTay = p.TenChucNangNhapTay,
                        Email = p.Email,
                        GhiChu = p.GhiChu,
                        UserId = p.UserId,
                        TenUser = p.UserId.HasValue
                        ? p.User.MaNhanVien + " — " + p.User.HoTen
                        : null
                    })
                    .ToList(),

                    Attachments = budgetRegistration.BudgetAttachments
                        .OrderByDescending(a => a.UploadedDate)
                        .Select(a => new BudgetAttachmentViewModel
                        {
                            AttachmentId = a.AttachmentId,
                            FileName = a.FileName ?? string.Empty,
                            FilePath = a.FilePath ?? string.Empty,
                            FileExtension = a.FileExtension ?? string.Empty,
                            FileSize = a.FileSize,
                            FileSizeFormatted = FormatFileSize(a.FileSize),
                            UploadedBy = a.User?.HoTen ?? "N/A",
                            UploadedDate = a.UploadedDate
                        })
                        .ToList(),
                };

                viewModel.CategoryTypeId = budgetRegistration.CategoryTypeId;
                viewModel.PriorityLevelId = budgetRegistration.PriorityLevelId;

                var approvalsBoSung = approvals
                    .Where(x => x.IsSupplementary)
                    .OrderBy(x => x.SupplementaryOrder)
                    .ToList();

                // Map Ngân sách gốc
                viewModel.NganSachGoc = new BudgetApprovalViewModel
                {
                    BudgetApprovalId = approvalGoc?.BudgetApprovalId ?? 0,
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    ProcessType = approvalGoc?.ProcessType ?? 1,
                    NgayDuyetPDA = approvalGoc?.NgayDuyetPDA,
                    NgayDuyetPKT = approvalGoc?.NgayDuyetPKT,
                    NgayDuyetERPD = approvalGoc?.NgayDuyetERPD,
                    NgayDuyetBTC = approvalGoc?.NgayDuyetBTC,
                    NgayDuyetBGD = approvalGoc?.NgayDuyetBGD,
                    DuToanGoc = budgetRegistration.DuToan,
                    DuToanPheDuyet = approvalGoc?.DuToanPheDuyet,
                    SoThongBao = approvalGoc?.SoThongBao,
                    //SoFMIO = approvalGoc?.SoFMIO,
                    SoFM = approvalGoc?.SoFM,
                    SoIO = approvalGoc?.SoIO,
                    TrangThaiPheDuyet = approvalGoc?.TrangThaiPheDuyet ?? 0,
                    IsSupplementary = false,
                    SupplementaryOrder = 0
                };

                // Map danh sách Đợt bổ sung
                viewModel.DanhSachBoSung = approvalsBoSung.Select(a =>
                    new BudgetApprovalViewModel
                    {
                        BudgetApprovalId = a.BudgetApprovalId,
                        BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                        ProcessType = a.ProcessType,
                        NgayDuyetPDA = a.NgayDuyetPDA,
                        NgayDuyetPKT = a.NgayDuyetPKT,
                        NgayDuyetERPD = a.NgayDuyetERPD,
                        NgayDuyetBTC = a.NgayDuyetBTC,
                        NgayDuyetBGD = a.NgayDuyetBGD,
                        DuToanPheDuyet = a.DuToanPheDuyet,
                        SoThongBao = a.SoThongBao,
                        TrangThaiPheDuyet = a.TrangThaiPheDuyet,
                        IsSupplementary = true,
                        SupplementaryOrder = a.SupplementaryOrder,
                        LyDoBoSung = a.LyDoBoSung,
                        NganSachBoSung = a.NganSachBoSung ?? 0
                    }).ToList();

                var config = budgetRegistration.ProgressConfigs.FirstOrDefault();
                viewModel.ThongTinTienDo = new ProgressConfigViewModel
                {
                    ProgressConfigId = config?.ProgressConfigId ?? 0,
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    TiTrongXayDung = config?.TiTrongXayDung ?? 0,
                    TiTrongKetCauThep = config?.TiTrongKetCauThep ?? 0,
                    TiTrongLapDatThietBi = config?.TiTrongLapDatThietBi ?? 0,
                    TiTrongHangMucKhac = config?.TiTrongHangMucKhac ?? 0,
                    DanhGiaChung = config?.DanhGiaChung,
                    TongTienDo = config?.TongTienDo
                };

                // Thêm vào viewModel
                viewModel.IsPhanNhiemUser = isPhanNhiemUser;

                // Kiểm tra chưa có NgayDuyetBGD (ngân sách gốc)
                bool chuaCoNgayDuyetBGD = approvalGoc == null
                                          || !approvalGoc.NgayDuyetBGD.HasValue;

                // Chủ hồ sơ
                bool isOwner = budgetRegistration.UserId == CurrentUser.UserId;


                return Json(new { success = true, data = viewModel, chuaCoNgayDuyetBGD = chuaCoNgayDuyetBGD, isOwner = isOwner },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDetailsModal Error - ID: {id}, Message: {ex.Message}");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi tải thông tin chi tiết." }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetDetailsModal(int? id)
        {
            try
            {
                // Validate id parameter
                if (!id.HasValue || id.Value <= 0)
                {
                    return Json(new { success = false, message = "Mã đăng ký không hợp lệ." }, JsonRequestBehavior.AllowGet);
                }

                // Validate current user
                if (CurrentUser == null)
                {
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." }, JsonRequestBehavior.AllowGet);
                }

                // Query budget registration with all related data
                var budgetRegistration = db.BudgetRegistrations
                .Include(x => x.PhongBan)
                .Include(x => x.User)
                .Include(x => x.BudgetCategoryType)
                .Include(x => x.BudgetPriorityLevel)
                .Include(x => x.BudgetAttachments.Select(a => a.User))
                .Include(x => x.ProjectArea)
                .Include(x => x.BudgetRegistrationPhanNhiems.Select(p => p.PhongBan))
                .Include(x => x.BudgetRegistrationPhanNhiems.Select(p => p.ChucNang_NhiemVu))
                .Include(x => x.BudgetApprovals)
                .Include(x => x.ProgressConfigs)
                .Include(x => x.BudgetRegistrationPhanNhiems.Select(p => p.User))
                .Include(x => x.BudgetInvestmentReason)
                .FirstOrDefault(x => x.BudgetRegistrationId == id.Value);

                // Check if record exists
                if (budgetRegistration == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin đăng ký." }, JsonRequestBehavior.AllowGet);
                }

                // 1. TÁI CẤU TRÚC: LOGIC XÁC ĐỊNH CHUỖI TEXT TRẠNG THÁI HIỂN THỊ MỚI
                var approvals = budgetRegistration.BudgetApprovals.ToList();
                var approvalGoc = approvals.FirstOrDefault(x => !x.IsSupplementary);
                var danhSachBoSung = approvals.Where(x => x.IsSupplementary).ToList();

                int? workflowType = budgetRegistration.WorkflowType;
                string trangThaiHienThiText = "Chưa xác định";

                // --- NHÓM 1: CHƯA XÁC NHẬN LUỒNG ---
                if (workflowType == null)
                {
                    trangThaiHienThiText = "Đăng ký mới"; // Case 1.1
                }
                // --- NHÓM 2: HỒ SƠ BỊ TRẢ LẠI ---
                else if (workflowType == 4)
                {
                    trangThaiHienThiText = "Chưa đủ hồ sơ"; // Case 2.1
                }
                // --- NHÓM 3: LUỒNG CHI PHÍ SẢN XUẤT ---
                else if (workflowType == 2)
                {
                    trangThaiHienThiText = "Theo luồng chi phí sản xuất"; // Case 3.1 -> 3.6
                }
                // --- NHÓM 4: LUỒNG NGÂN SÁCH ĐẦU TƯ (WorkflowType = 1) ---
                else if (workflowType == 1)
                {
                    if (!danhSachBoSung.Any())
                    {
                        if (approvalGoc?.NgayDuyetPDA == null && approvalGoc?.NgayDuyetBGD == null)
                            trangThaiHienThiText = "Chưa đủ hồ sơ"; // Case 4.1
                        else if (approvalGoc?.NgayDuyetPDA != null && approvalGoc?.NgayDuyetBGD == null)
                            trangThaiHienThiText = "Đang thực hiện xin ngân sách"; // Case 4.2
                        else if (approvalGoc?.NgayDuyetPDA != null && approvalGoc?.NgayDuyetBGD != null)
                            trangThaiHienThiText = "Đã phê duyệt ngân sách"; // Case 4.3
                    }
                    else
                    {
                        bool boSungChuaDuyetGi = danhSachBoSung.Any(a => a.NgayDuyetPDA == null && a.NgayDuyetBGD == null);
                        bool boSungDaDuyetPDA_ChuaBGD = danhSachBoSung.Any(a => a.NgayDuyetPDA != null && a.NgayDuyetBGD == null);
                        bool boSungDaDuyetCaHai = danhSachBoSung.Any(a => a.NgayDuyetPDA != null && a.NgayDuyetBGD != null);

                        if (boSungChuaDuyetGi)
                            trangThaiHienThiText = "Đã phê duyệt ngân sách"; // Case 4.4
                        else if (boSungDaDuyetPDA_ChuaBGD)
                            trangThaiHienThiText = "Đang bổ sung ngân sách"; // Case 4.5
                        else if (boSungDaDuyetCaHai)
                            trangThaiHienThiText = "Đã phê duyệt ngân sách"; // Case 4.6
                    }
                }

                // Check permissions
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;

                bool isPhanNhiemUser =
                    !isManagerOrAdmin
                    && budgetRegistration.UserId != CurrentUser.UserId
                    && budgetRegistration.BudgetRegistrationPhanNhiems
                        .Any(p => p.UserId == CurrentUser.UserId);

                if (!isManagerOrAdmin && budgetRegistration.UserId != CurrentUser.UserId && !isPhanNhiemUser)
                {
                    return Json(new { success = false, message = "Bạn không có quyền xem đăng ký này." }, JsonRequestBehavior.AllowGet);
                }

                // Map to view model
                var viewModel = new BudgetRegistrationDetailsViewModel
                {
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    MaHangMuc = budgetRegistration.MaHangMuc ?? string.Empty,
                    TenHangMuc = budgetRegistration.TenHangMuc ?? string.Empty,
                    DuToan = budgetRegistration.DuToan ?? 0,
                    SoToTrinh = budgetRegistration.SoToTrinh ?? string.Empty,
                    SoLuong = budgetRegistration.SoLuong,
                    LyDoDauTu = budgetRegistration.LyDoDauTu ?? string.Empty,
                    InvestmentReasonId = budgetRegistration.InvestmentReasonId,
                    IsManualReason = budgetRegistration.BudgetInvestmentReason?.IsManual ?? (budgetRegistration.InvestmentReasonId == null),
                    MoTaKyThuat = budgetRegistration.MoTaKyThuat,
                    LinkTaiLieuLienQuan = budgetRegistration.LinkTaiLieuLienQuan,

                    CategoryTypeName = budgetRegistration.BudgetCategoryType?.CategoryTypeName ?? "N/A",
                    PriorityLevelName = budgetRegistration.BudgetPriorityLevel?.PriorityLevelName ?? "N/A",

                    NgayBatDau = budgetRegistration.NgayBatDau,
                    NgayKetThuc = budgetRegistration.NgayKetThuc,
                    NgayTao = budgetRegistration.CreatedDate,

                    TenPhongBan = budgetRegistration.PhongBan.TenPhongBan,

                    UserId = budgetRegistration.UserId,

                    // GÁN THUỘC TÍNH MỚI CHO VIEW MODEL ĐỂ TRẢ VỀ AJAX
                    TrangThaiHienThiText = trangThaiHienThiText,

                    TenNguoiDangKy = budgetRegistration.User.HoTen,
                    WorkflowType = budgetRegistration.WorkflowType,
                    IsManagerOrAdmin = isManagerOrAdmin,
                    ProjectAreaId = budgetRegistration.ProjectAreaId,

                    // Phân nhiệm
                    DanhSachPhanNhiem = budgetRegistration.BudgetRegistrationPhanNhiems
                    .Select(p => new PhanNhiemViewModel
                    {
                        PhongBanId = p.PhongBanId,
                        TenPhongBan = p.PhongBan?.TenPhongBan,
                        ChucNangNhiemVuId = p.ChucNangNhiemVuId,
                        TenChucNang = p.ChucNang_NhiemVu?.TenChucNang,
                        TenChucNangNhapTay = p.TenChucNangNhapTay,
                        Email = p.Email,
                        GhiChu = p.GhiChu,
                        UserId = p.UserId,
                        TenUser = p.UserId.HasValue ? p.User.MaNhanVien + " — " + p.User.HoTen : null
                    })
                    .ToList(),

                    Attachments = budgetRegistration.BudgetAttachments
                        .OrderByDescending(a => a.UploadedDate)
                        .Select(a => new BudgetAttachmentViewModel
                        {
                            AttachmentId = a.AttachmentId,
                            FileName = a.FileName ?? string.Empty,
                            FilePath = a.FilePath ?? string.Empty,
                            FileExtension = a.FileExtension ?? string.Empty,
                            FileSize = a.FileSize,
                            FileSizeFormatted = FormatFileSize(a.FileSize),
                            UploadedBy = a.User?.HoTen ?? "N/A",
                            UploadedDate = a.UploadedDate
                        })
                        .ToList(),
                };

                viewModel.CategoryTypeId = budgetRegistration.CategoryTypeId;
                viewModel.PriorityLevelId = budgetRegistration.PriorityLevelId;

                var approvalsBoSung = approvals
                    .Where(x => x.IsSupplementary)
                    .OrderBy(x => x.SupplementaryOrder)
                    .ToList();

                // Map Ngân sách gốc
                viewModel.NganSachGoc = new BudgetApprovalViewModel
                {
                    BudgetApprovalId = approvalGoc?.BudgetApprovalId ?? 0,
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    ProcessType = approvalGoc?.ProcessType ?? 1,
                    NgayDuyetPDA = approvalGoc?.NgayDuyetPDA,
                    NgayDuyetPKT = approvalGoc?.NgayDuyetPKT,
                    NgayDuyetERPD = approvalGoc?.NgayDuyetERPD,
                    NgayDuyetBTC = approvalGoc?.NgayDuyetBTC,
                    NgayDuyetBGD = approvalGoc?.NgayDuyetBGD,
                    DuToanGoc = budgetRegistration.DuToan,
                    DuToanPheDuyet = approvalGoc?.DuToanPheDuyet,
                    SoThongBao = approvalGoc?.SoThongBao,
                    SoFM = approvalGoc?.SoFM,
                    SoIO = approvalGoc?.SoIO,
                    TrangThaiPheDuyet = approvalGoc?.TrangThaiPheDuyet ?? 0,
                    IsSupplementary = false,
                    SupplementaryOrder = 0
                };

                // Map danh sách Đợt bổ sung
                viewModel.DanhSachBoSung = approvalsBoSung.Select(a =>
                    new BudgetApprovalViewModel
                    {
                        BudgetApprovalId = a.BudgetApprovalId,
                        BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                        ProcessType = a.ProcessType,
                        NgayDuyetPDA = a.NgayDuyetPDA,
                        NgayDuyetPKT = a.NgayDuyetPKT,
                        NgayDuyetERPD = a.NgayDuyetERPD,
                        NgayDuyetBTC = a.NgayDuyetBTC,
                        NgayDuyetBGD = a.NgayDuyetBGD,
                        DuToanPheDuyet = a.DuToanPheDuyet,
                        SoThongBao = a.SoThongBao,
                        TrangThaiPheDuyet = a.TrangThaiPheDuyet,
                        IsSupplementary = true,
                        SupplementaryOrder = a.SupplementaryOrder,
                        LyDoBoSung = a.LyDoBoSung,
                        NganSachBoSung = a.NganSachBoSung ?? 0
                    }).ToList();

                var config = budgetRegistration.ProgressConfigs.FirstOrDefault();
                viewModel.ThongTinTienDo = new ProgressConfigViewModel
                {
                    ProgressConfigId = config?.ProgressConfigId ?? 0,
                    BudgetRegistrationId = budgetRegistration.BudgetRegistrationId,
                    TiTrongXayDung = config?.TiTrongXayDung ?? 0,
                    TiTrongKetCauThep = config?.TiTrongKetCauThep ?? 0,
                    TiTrongLapDatThietBi = config?.TiTrongLapDatThietBi ?? 0,
                    TiTrongHangMucKhac = config?.TiTrongHangMucKhac ?? 0,
                    DanhGiaChung = config?.DanhGiaChung,
                    TongTienDo = config?.TongTienDo
                };

                viewModel.IsPhanNhiemUser = isPhanNhiemUser;

                // Kiểm tra chưa có NgayDuyetBGD (ngân sách gốc)
                bool chuaCoNgayDuyetBGD = approvalGoc == null || !approvalGoc.NgayDuyetBGD.HasValue;

                // Chủ hồ sơ
                bool isOwner = budgetRegistration.UserId == CurrentUser.UserId;

                return Json(new { success = true, data = viewModel, chuaCoNgayDuyetBGD = chuaCoNgayDuyetBGD, isOwner = isOwner }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDetailsModal Error - ID: {id}, Message: {ex.Message}");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi tải thông tin chi tiết." }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult SaveDetailsModal(SaveDetailsModalViewModel model)
        {
            try
            {
                if (CurrentUser == null)
                    return Json(new
                    {
                        success = false,
                        message = "Phiên đăng nhập đã hết hạn."
                    });

                var errors = ModelState
                    .Where(x => x.Value.Errors.Any())
                    .Select(x => new {
                        Field = x.Key,
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage)
                    });
                System.Diagnostics.Debug.WriteLine(
                    Newtonsoft.Json.JsonConvert.SerializeObject(errors));

                if (!ModelState.IsValid)
                    return Json(new
                    {
                        success = false,
                        message = "Dữ liệu không hợp lệ."
                    });

                var entity = db.BudgetRegistrations
                    .FirstOrDefault(x => x.BudgetRegistrationId
                                       == model.BudgetRegistrationId);

                if (entity == null)
                    return Json(new
                    {
                        success = false,
                        message = "Không tìm thấy hồ sơ."
                    });

                bool isManagerOrAdmin =
                    CurrentUser.RoleId == Constants.RoleConst.Admin ||
                    CurrentUser.RoleId == Constants.RoleConst.Manager;

                // *** THÊM MỚI: Kiểm tra user thường có phải chủ hồ sơ không ***
                bool isOwner = entity.UserId == CurrentUser.UserId;

                bool isPhanNhiemUser =
                    !isManagerOrAdmin
                    && entity.UserId != CurrentUser.UserId
                    && db.BudgetRegistrationPhanNhiems
                        .Any(p => p.BudgetRegistrationId == model.BudgetRegistrationId
                               && p.UserId == CurrentUser.UserId);

                var statusHelper = new BudgetRegistrationListViewModel
                {
                    SoToTrinhRaw = entity.SoToTrinh,
                    WorkflowType = entity.WorkflowType,
                    TrangThaiPheDuyetGoc = db.BudgetApprovals.FirstOrDefault(a => a.BudgetRegistrationId == entity.BudgetRegistrationId && !a.IsSupplementary)?.TrangThaiPheDuyet ?? 0,
                    CoBoSungChuaDuyet = db.BudgetApprovals.Any(a => a.BudgetRegistrationId == entity.BudgetRegistrationId && a.IsSupplementary && a.TrangThaiPheDuyet != 2)
                };
                int currentStatus = statusHelper.TrangThaiHienThi;

                bool canOwnerEdit = isOwner && (currentStatus == 0 || currentStatus == 1 || currentStatus == 6);

                // KIỂM TRA QUYỀN SỬA TAB PHIẾU
                if (isManagerOrAdmin || canOwnerEdit)
                {
                    entity.ProjectAreaId = model.ProjectAreaId;
                    entity.TenHangMuc = model.TenHangMuc?.Trim();
                    entity.DuToan = model.DuToan;
                    entity.SoToTrinh = model.SoToTrinh?.Trim();
                    entity.CategoryTypeId = model.CategoryTypeId;
                    entity.PriorityLevelId = model.PriorityLevelId;
                    entity.SoLuong = model.SoLuong;
                    entity.NgayBatDau = model.NgayBatDau;
                    entity.NgayKetThuc = model.NgayKetThuc;
                    entity.LyDoDauTu = model.LyDoDauTu?.Trim();
                    entity.MoTaKyThuat = model.MoTaKyThuat?.Trim();
                    entity.LinkTaiLieuLienQuan = model.LinkTaiLieuLienQuan?.Trim();

                    // Cập nhật lại trạng thái hồ sơ nếu cần (DetermineHoSoStatus)
                    // entity.TrangThai = DetermineHoSoStatus(entity); 
                }

                // Chặn nếu không có quyền
                if (!isManagerOrAdmin
                 && entity.UserId != CurrentUser.UserId
                 && !isPhanNhiemUser)
                    return Json(new
                    {
                        success = false,
                        message = "Bạn không có quyền thực hiện."
                    });

                // User phân nhiệm không được lưu WorkflowType + Phê duyệt + Phân nhiệm
                // Chỉ được lưu Nhật ký tiến độ
                //if (isPhanNhiemUser)
                //{
                //    // Chỉ lưu Nhật ký tiến độ
                //    SaveProgressData(model.BudgetRegistrationId,
                //                     model.ThongTinTienDo,
                //                     CurrentUser.UserId); // *** Truyền UserId ***
                //    db.SaveChanges();
                //    return Json(new
                //    {
                //        success = true,
                //        message = "Lưu thông tin thành công."
                //    });
                //}

                // ================================================================
                // VÙNG CHỈ ADMIN/MANAGER — WorkflowType + Phê duyệt
                // ================================================================
                if (isManagerOrAdmin)
                {
                    // Lưu WorkflowType
                    entity.WorkflowType = model.WorkflowType;

                    // *** THÊM MỚI: Xử lý MaHangMuc theo WorkflowType ***
                    if (model.WorkflowType == 1)
                    {
                        // Chỉ generate nếu chưa có mã
                        if (string.IsNullOrEmpty(entity.MaHangMuc))
                        {
                            // Lấy thông tin phòng ban của chủ hồ sơ
                            var chuHoSo = db.Users
                                .Include(u => u.PhongBan)
                                .FirstOrDefault(u => u.UserId == entity.UserId);

                            if (chuHoSo?.PhongBan != null)
                            {
                                entity.MaHangMuc = GenerateMaHangMucForUser(
                                    chuHoSo.PhongBan.MaPhongBan,
                                    chuHoSo.PhongBanId ?? 0,
                                    entity.CategoryTypeId);
                            }
                        }
                    }
                    else
                    {
                        // WorkflowType != 1 → reset MaHangMuc
                        entity.MaHangMuc = null;
                    }

                    // Hàm tính trạng thái dùng chung
                    Func<BudgetApprovalViewModel, int> tinhTrangThai = (pd) =>
                    {
                        if (pd.NgayDuyetBGD.HasValue) return 2;
                        if (pd.NgayDuyetPDA.HasValue
                         || pd.NgayDuyetPKT.HasValue
                         || pd.NgayDuyetERPD.HasValue
                         || pd.NgayDuyetBTC.HasValue) return 1;
                        return 0;
                    };

                    // Xóa toàn bộ BudgetApprovals cũ và insert lại
                    var oldApprovals = db.BudgetApprovals
                        .Where(x => x.BudgetRegistrationId
                                   == model.BudgetRegistrationId)
                        .ToList();
                    db.BudgetApprovals.RemoveRange(oldApprovals);

                    // Insert Ngân sách gốc
                    if (model.NganSachGoc != null)
                    {
                        var pd = model.NganSachGoc;
                        db.BudgetApprovals.Add(new BudgetApproval
                        {
                            BudgetRegistrationId = model.BudgetRegistrationId,
                            ProcessType = pd.ProcessType,
                            NgayDuyetPDA = pd.NgayDuyetPDA,
                            NgayDuyetPKT = pd.NgayDuyetPKT,
                            NgayDuyetERPD = pd.NgayDuyetERPD,
                            NgayDuyetBTC = pd.NgayDuyetBTC,
                            NgayDuyetBGD = pd.NgayDuyetBGD,
                            DuToanPheDuyet = pd.DuToanPheDuyet,
                            SoThongBao = pd.SoThongBao?.Trim(),
                            //SoFMIO = pd.SoFMIO?.Trim(),
                            SoFM = pd.SoFM?.Trim(),
                            SoIO = pd.SoIO?.Trim(),
                            TrangThaiPheDuyet = tinhTrangThai(pd),
                            IsSupplementary = false,
                            SupplementaryOrder = 0,
                            LyDoBoSung = null,
                            NganSachBoSung = null
                        });
                    }

                    // Insert các Đợt bổ sung
                    if (model.DanhSachBoSung != null && model.DanhSachBoSung.Any())
                    {
                        int order = 1;
                        foreach (var pd in model.DanhSachBoSung)
                        {
                            db.BudgetApprovals.Add(new BudgetApproval
                            {
                                BudgetRegistrationId = model.BudgetRegistrationId,
                                ProcessType = pd.ProcessType,
                                NgayDuyetPDA = pd.NgayDuyetPDA,
                                NgayDuyetPKT = pd.NgayDuyetPKT,
                                NgayDuyetERPD = pd.NgayDuyetERPD,
                                NgayDuyetBTC = pd.NgayDuyetBTC,
                                NgayDuyetBGD = pd.NgayDuyetBGD,
                                DuToanPheDuyet = pd.DuToanPheDuyet,
                                SoThongBao = pd.SoThongBao?.Trim(),
                                //SoFMIO = pd.SoFMIO?.Trim(),
                                TrangThaiPheDuyet = tinhTrangThai(pd),
                                IsSupplementary = true,
                                SupplementaryOrder = order++,
                                LyDoBoSung = pd.LyDoBoSung?.Trim(),
                                NganSachBoSung = pd.NganSachBoSung ?? 0
                            });
                        }
                    }
                }

                // ================================================================
                // VÙNG ADMIN/MANAGER + CHỦ HỒ SƠ — Phân nhiệm + Nhật ký tiến độ
                // ================================================================

                // Xóa toàn bộ Phân nhiệm cũ và insert lại
                if (!model.IsPhanNhiemSearchMode)
                {
                    // Xóa toàn bộ Phân nhiệm cũ và insert lại
                    var oldPhanNhiem = db.BudgetRegistrationPhanNhiems
                        .Where(x => x.BudgetRegistrationId == model.BudgetRegistrationId)
                        .ToList();
                    db.BudgetRegistrationPhanNhiems.RemoveRange(oldPhanNhiem);

                    if (model.DanhSachPhanNhiem != null && model.DanhSachPhanNhiem.Any())
                    {
                        foreach (var pn in model.DanhSachPhanNhiem)
                        {
                            if (pn.PhongBanId == null) continue;

                            db.BudgetRegistrationPhanNhiems.Add(
                                new BudgetRegistrationPhanNhiem
                                {
                                    BudgetRegistrationId = model.BudgetRegistrationId,
                                    PhongBanId = pn.PhongBanId.Value,
                                    ChucNangNhiemVuId = pn.ChucNangNhiemVuId,
                                    TenChucNangNhapTay = pn.TenChucNangNhapTay?.Trim(),
                                    Email = pn.Email?.Trim(),
                                    GhiChu = pn.GhiChu?.Trim(),
                                    UserId = pn.UserId
                                });
                        }
                    }
                }

                //SaveProgressData(model.BudgetRegistrationId, model.ThongTinTienDo, CurrentUser.UserId);
                if (model.NhatKyTienDo != null)
                {
                    _SaveProgressLog(model.NhatKyTienDo);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Lưu thông tin thành công."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveDetailsModal Error: {ex.Message}");
                return Json(new { success = false, message = "Error: Đã xảy ra lỗi khi lưu thông tin." });
            }
        }

        // ================================================================
        // POST: /Budget/SubmitEditRequest
        // User gửi thông tin chỉnh sửa chủ động (RevisionType = 2)
        // ================================================================
        [HttpPost]
        public JsonResult SubmitEditRequest_OLD(SubmitRevisionDto dto)
        {
            if (dto == null || dto.BudgetRegistrationId <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var budget = db.BudgetRegistrations.Find(dto.BudgetRegistrationId);
            if (budget == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            // Chỉ chủ hồ sơ mới được gửi
            if (budget.UserId != CurrentUser.UserId)
                return Json(new { success = false, message = "Không có quyền thực hiện." });

            // Kiểm tra chưa có NgayDuyetBGD
            var approvalGoc = db.BudgetApprovals
                                .FirstOrDefault(a => a.BudgetRegistrationId == dto.BudgetRegistrationId
                                                  && !a.IsSupplementary);
            if (approvalGoc != null && approvalGoc.NgayDuyetBGD.HasValue)
                return Json(new
                {
                    success = false,
                    message = "Hồ sơ đã được duyệt BGĐ, không thể chỉnh sửa."
                });

            if (string.IsNullOrWhiteSpace(dto.LyDoChinhSua))
                return Json(new { success = false, message = "Vui lòng nhập lý do chỉnh sửa." });

            var now = DateTime.Now;

            // Tìm revision type=2 đang chờ duyệt → đè lên
            var revision = db.BudgetRevisions
                             .FirstOrDefault(r => r.BudgetRegistrationId == dto.BudgetRegistrationId
                                               && r.RevisionType == 2
                                               && r.RevisionStatus == 1);
            if (revision == null)
            {
                revision = new BudgetRevision
                {
                    BudgetRegistrationId = dto.BudgetRegistrationId,
                    RevisionType = 2,
                    CreatedBy = CurrentUser.UserId,
                    CreatedDate = now
                };
                db.BudgetRevisions.Add(revision);
            }

            revision.ProjectAreaId = dto.ProjectAreaId;
            revision.TenHangMuc = dto.TenHangMuc;
            revision.DuToan = dto.DuToan;
            revision.SoToTrinh = dto.SoToTrinh;
            revision.CategoryTypeId = dto.CategoryTypeId;
            revision.PriorityLevelId = dto.PriorityLevelId;
            revision.SoLuong = dto.SoLuong;
            revision.LyDoDauTu = dto.LyDoDauTu;
            revision.MoTaKyThuat = dto.MoTaKyThuat;
            revision.NgayBatDau = string.IsNullOrEmpty(dto.NgayBatDau)
                                             ? (DateTime?)null
                                             : DateTime.Parse(dto.NgayBatDau);
            revision.NgayKetThuc = string.IsNullOrEmpty(dto.NgayKetThuc)
                                             ? (DateTime?)null
                                             : DateTime.Parse(dto.NgayKetThuc);
            revision.LinkTaiLieuLienQuan = dto.LinkTaiLieuLienQuan;
            revision.LyDoChinhSua = dto.LyDoChinhSua?.Trim();
            revision.RevisionStatus = 1;
            revision.CreatedDate = now;

            db.SaveChanges();

            var revFiles = Request.Files.GetMultiple("HoSoCanCu")
                            .Where(f => f != null && f.ContentLength > 0)
                            .ToList();

            if (revFiles.Any())
            {
                // Xóa file revision cũ (nếu đang đè lên revision cũ)
                var oldRevAttachments = db.BudgetRevisionAttachments
                                          .Where(a => a.RevisionId == revision.RevisionId)
                                          .ToList();
                foreach (var old in oldRevAttachments)
                {
                    try
                    {
                        var physicalPath = Server.MapPath("~" + old.FilePath);
                        if (System.IO.File.Exists(physicalPath))
                            System.IO.File.Delete(physicalPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"SubmitEditRequest - Xóa RevisionAttachment lỗi [{old.FilePath}]: {ex.Message}");
                    }
                }
                db.BudgetRevisionAttachments.RemoveRange(oldRevAttachments);
                db.SaveChanges();

                // Lưu file mới vào BudgetRevisionAttachments
                // KHÔNG xóa BudgetAttachments — chờ Admin duyệt mới xóa
                foreach (var revFile in revFiles)
                    _SaveRevisionAttachment(revFile, revision.RevisionId);
            }

            // Gửi thông báo đến tất cả Admin
            var adminUsers = db.Users
                               .Where(u => u.RoleId == 1)
                               .ToList();
            foreach (var admin in adminUsers)
            {
                NotificationHelper.Send(
                    db,
                    toUserId: admin.UserId,
                    title: "User đã chỉnh sửa hồ sơ",
                    message: $"Hồ sơ \"{budget.TenHangMuc}\" vừa được chỉnh sửa. Vui lòng xem xét và xác nhận.",
                    url: $"/Budget/Revisions?openRevision={revision.RevisionId}",
                    relatedRevisionId: revision.RevisionId
                );
            }

            return Json(new
            {
                success = true,
                message = "Gửi thông tin chỉnh sửa thành công. Vui lòng chờ xác nhận."
            });
        }

        [HttpPost]
        public JsonResult SubmitEditRequest(SubmitRevisionDto dto)
        {
            if (dto == null || dto.BudgetRegistrationId <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var budget = db.BudgetRegistrations.Find(dto.BudgetRegistrationId);
            if (budget == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            // Chỉ chủ hồ sơ mới được gửi
            if (budget.UserId != CurrentUser.UserId)
                return Json(new { success = false, message = "Không có quyền thực hiện." });

            // Kiểm tra chưa có NgayDuyetBGD
            var approvalGoc = db.BudgetApprovals
                                .FirstOrDefault(a => a.BudgetRegistrationId == dto.BudgetRegistrationId
                                                  && !a.IsSupplementary);
            if (approvalGoc != null && approvalGoc.NgayDuyetBGD.HasValue)
                return Json(new
                {
                    success = false,
                    message = "Hồ sơ đã được duyệt BGĐ, không thể chỉnh sửa."
                });

            if (string.IsNullOrWhiteSpace(dto.LyDoChinhSua))
                return Json(new { success = false, message = "Vui lòng nhập lý do chỉnh sửa." });

            var now = DateTime.Now;

            // Tìm hoặc khởi tạo bản ghi BudgetRevisions
            var revision = db.BudgetRevisions
                             .FirstOrDefault(r => r.BudgetRegistrationId == dto.BudgetRegistrationId
                                               && r.RevisionType == 2
                                               && r.RevisionStatus == 1);
            if (revision == null)
            {
                revision = new BudgetRevision
                {
                    BudgetRegistrationId = dto.BudgetRegistrationId,
                    RevisionType = 2,
                    CreatedBy = CurrentUser.UserId,
                    CreatedDate = now
                };
                db.BudgetRevisions.Add(revision);
            }

            // ── LOGIC XỬ LÝ CHUẨN HÓA LÝ DO ĐẦU TƯ MỚI TRƯỚC KHI LƯU ──
            bool isManualReason = false;
            if (dto.InvestmentReasonId.HasValue)
            {
                var reasonCategory = db.BudgetInvestmentReasons.Find(dto.InvestmentReasonId.Value);
                if (reasonCategory != null && reasonCategory.IsManual)
                {
                    isManualReason = true;
                }
            }

            if (isManualReason)
            {
                // Nếu chọn Điền thủ công: Lưu chuỗi nhập tay, ID danh mục bằng NULL
                revision.InvestmentReasonId = null;
                revision.LyDoDauTu = dto.LyDoDauTu?.Trim();
            }
            else
            {
                // Nếu chọn danh mục có sẵn: Lưu khóa ngoại, làm sạch trường chuỗi nhập tay
                revision.InvestmentReasonId = dto.InvestmentReasonId;
                revision.LyDoDauTu = null;
            }

            // Gán các trường dữ liệu còn lại từ dto sang revision
            revision.ProjectAreaId = dto.ProjectAreaId;
            revision.TenHangMuc = dto.TenHangMuc;
            revision.DuToan = dto.DuToan;
            revision.SoToTrinh = dto.SoToTrinh;
            revision.CategoryTypeId = dto.CategoryTypeId;
            revision.PriorityLevelId = dto.PriorityLevelId;
            revision.SoLuong = dto.SoLuong;

            // Đoạn gán cũ [revision.LyDoDauTu = dto.LyDoDauTu] xóa đi vì đã được xử lý tối ưu ở khối logic phía trên

            revision.MoTaKyThuat = dto.MoTaKyThuat;
            revision.NgayBatDau = string.IsNullOrEmpty(dto.NgayBatDau)
                                    ? (DateTime?)null
                                    : DateTime.Parse(dto.NgayBatDau);
            revision.NgayKetThuc = string.IsNullOrEmpty(dto.NgayKetThuc)
                                    ? (DateTime?)null
                                    : DateTime.Parse(dto.NgayKetThuc);
            revision.LinkTaiLieuLienQuan = dto.LinkTaiLieuLienQuan;
            revision.LyDoChinhSua = dto.LyDoChinhSua?.Trim();
            revision.RevisionStatus = 1;
            revision.CreatedDate = now;

            db.SaveChanges();

            var revFiles = Request.Files.GetMultiple("HoSoCanCu")
                            .Where(f => f != null && f.ContentLength > 0)
                            .ToList();

            if (revFiles.Any())
            {
                // Xóa file revision cũ (nếu đang đè lên revision cũ)
                var oldRevAttachments = db.BudgetRevisionAttachments
                                          .Where(a => a.RevisionId == revision.RevisionId)
                                          .ToList();
                foreach (var old in oldRevAttachments)
                {
                    try
                    {
                        var physicalPath = Server.MapPath("~" + old.FilePath);
                        if (System.IO.File.Exists(physicalPath))
                            System.IO.File.Delete(physicalPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"SubmitEditRequest - Xóa RevisionAttachment lỗi [{old.FilePath}]: {ex.Message}");
                    }
                }
                db.BudgetRevisionAttachments.RemoveRange(oldRevAttachments);
                db.SaveChanges();

                // Lưu file mới vào BudgetRevisionAttachments
                // KHÔNG xóa BudgetAttachments — chờ Admin duyệt mới xóa
                foreach (var revFile in revFiles)
                    _SaveRevisionAttachment(revFile, revision.RevisionId);
            }

            // Gửi thông báo đến tất cả Admin
            var adminUsers = db.Users
                               .Where(u => u.RoleId == 1)
                               .ToList();
            foreach (var admin in adminUsers)
            {
                NotificationHelper.Send(
                    db,
                    toUserId: admin.UserId,
                    title: "User đã chỉnh sửa hồ sơ",
                    message: $"Hồ sơ \"{budget.TenHangMuc}\" vừa được chỉnh sửa. Vui lòng xem xét và xác nhận.",
                    url: $"/Budget/Revisions?openRevision={revision.RevisionId}",
                    relatedRevisionId: revision.RevisionId
                );
            }

            return Json(new
            {
                success = true,
                message = "Gửi thông tin chỉnh sửa thành công. Vui lòng chờ xác nhận."
            });
        }

        // Tách logic lưu nhật ký tiến độ thành private method
        // để dùng chung cho cả SaveDetailsModal và SaveProgressLog
        private void _SaveProgressLog(SaveProgressLogDto dto)
        {
            var now = DateTime.Now;

            var log = db.ProgressLogs
                        .FirstOrDefault(p => p.BudgetRegistrationId == dto.BudgetRegistrationId);

            if (log == null)
            {
                log = new ProgressLog
                {
                    BudgetRegistrationId = dto.BudgetRegistrationId,
                    CreatedDate = now
                };
                db.ProgressLogs.Add(log);
                db.SaveChanges();
            }
            else
            {
                var oldAreaIds = db.ProgressLogAreas
                                   .Where(a => a.ProgressLogId == log.ProgressLogId)
                                   .Select(a => a.ProgressLogAreaId)
                                   .ToList();

                db.ProgressItems
                  .RemoveRange(db.ProgressItems
                                 .Where(i => oldAreaIds.Contains(i.ProgressLogAreaId)));

                db.ProgressLogAreas
                  .RemoveRange(db.ProgressLogAreas
                                 .Where(a => a.ProgressLogId == log.ProgressLogId));

                log.UpdatedDate = now;
            }

            log.DanhGiaChung = dto.DanhGiaChung;
            log.LuuY = dto.LuuY;
            log.TongTienDo = dto.TongTienDo;

            int areaOrder = 0;
            foreach (var areaDto in dto.Areas ?? new List<ProgressLogAreaDto>())
            {
                var area = new ProgressLogArea
                {
                    ProgressLogId = log.ProgressLogId,
                    TenKhuVuc = areaDto.TenKhuVuc,
                    TienDo = areaDto.TienDo,
                    SortOrder = areaOrder++
                };
                db.ProgressLogAreas.Add(area);
                db.SaveChanges();

                int itemOrder = 0;
                foreach (var itemDto in areaDto.Items ?? new List<ProgressItemDto>())
                {
                    db.ProgressItems.Add(new ProgressItem
                    {
                        ProgressLogAreaId = area.ProgressLogAreaId,
                        WorkItemId = itemDto.WorkItemId,
                        TenHangMuc = itemDto.TenHangMuc,
                        DonViTinh = itemDto.DonViTinh,
                        KLKeHoach = itemDto.KLKeHoach,
                        KLThucTe = itemDto.KLThucTe,
                        PhanTram = itemDto.PhanTram,
                        NgayBatDauKH = itemDto.NgayBatDauKH,
                        NgayKetThucKH = itemDto.NgayKetThucKH,
                        GhiChu = itemDto.GhiChu,
                        SortOrder = itemOrder++
                    });
                }
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Helper method để format file size thành dạng dễ đọc
        /// </summary>
        /// <param name="bytes">Kích thước file tính bằng bytes</param>
        /// <returns>Chuỗi đã format (VD: 1.5 MB, 250 KB)</returns>
        private string FormatFileSize(long bytes)
        {
            try
            {
                if (bytes <= 0)
                    return "0 B";

                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double size = bytes;

                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }

                return $"{size:0.##} {sizes[order]}";
            }
            catch
            {
                return "N/A";
            }
        }

        /// <summary>
        /// GET: Budget/GetFileContent/5 - Lấy nội dung file để hiển thị
        /// </summary>
        [HttpGet]
        public ActionResult GetFileContent(int? id)
        {
            try
            {
                if (!id.HasValue || id.Value <= 0)
                    return HttpNotFound();

                if (CurrentUser == null)
                    return new HttpUnauthorizedResult();

                var attachment = db.BudgetAttachments
                    .Include(x => x.BudgetRegistration)
                    .FirstOrDefault(x => x.AttachmentId == id.Value);

                if (attachment == null)
                    return HttpNotFound();

                // Check permissions
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;

                if (!isManagerOrAdmin && attachment.BudgetRegistration.UserId != CurrentUser.UserId)
                    return new HttpUnauthorizedResult();

                string uploadsFolder = Server.MapPath("~/Uploads/HoSoCanCu/");
                string fullPath = Path.Combine(uploadsFolder, attachment.FilePath);

                if (!System.IO.File.Exists(fullPath))
                    return HttpNotFound();

                string extension = attachment.FileExtension?.TrimStart('.').ToLower() ?? string.Empty;
                string contentType = GetContentType(extension);

                byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);

                // Set headers for inline display
                Response.AddHeader("Content-Disposition", $"inline; filename=\"{attachment.FileName}\"");

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFileContent Error - ID: {id}, Message: {ex.Message}");
                return new HttpStatusCodeResult(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// GET: Budget/DownloadFile/5 - Tải xuống file
        /// </summary>
        // ================================================================
        // GET: /Budget/DownloadFile?attachmentId=xxx
        // ================================================================
        [HttpGet]
        public ActionResult DownloadFile(int attachmentId)
        {
            var attachment = db.BudgetAttachments.Find(attachmentId);
            if (attachment == null)
                return HttpNotFound();

            // Kiểm tra quyền: chỉ người liên quan mới download được
            var budget = db.BudgetRegistrations
                           .Find(attachment.BudgetRegistrationId);
            if (budget == null)
                return HttpNotFound();

            bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin
                                 || CurrentUser.RoleId == Constants.RoleConst.Manager;
            bool isOwner = budget.UserId == CurrentUser.UserId;
            bool isPhanNhiem = db.BudgetRegistrationPhanNhiems
                                 .Any(p => p.BudgetRegistrationId == budget.BudgetRegistrationId
                                        && p.UserId == CurrentUser.UserId);

            if (!isManagerOrAdmin && !isOwner && !isPhanNhiem)
                return new HttpStatusCodeResult(403);

            // Xây dựng đường dẫn vật lý
            var physicalPath = Server.MapPath("~" + attachment.FilePath);
            if (!System.IO.File.Exists(physicalPath))
                return HttpNotFound("File không tồn tại trên server.");

            // Xác định content type
            var contentType = _GetContentType(attachment.FileExtension);

            return File(physicalPath, contentType,
                        attachment.FileName); // FileName là tên gốc khi download
        }

        private string _GetContentType(string extension)
        {
            switch (extension?.ToLower())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }

        private string GenerateMaHangMucForUser(string maPhongBan,
                                         int phongBanId,
                                         int loaiHangMucId = 1)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(maPhongBan)
                 || phongBanId <= 0) return string.Empty;

                if (loaiHangMucId <= 0) loaiHangMucId = 1;

                string currentMonthYear = DateTime.Now.ToString("MMyy");
                int sequence = CalculateSequence(
                    phongBanId, loaiHangMucId, currentMonthYear);

                string maPB = maPhongBan.Trim().ToUpper();
                string loaiHM = loaiHangMucId.ToString("D2");
                string sequenceFormatted = sequence.ToString("D2");

                return $"{maPB}.{loaiHM}.{currentMonthYear}.{sequenceFormatted}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"GenerateMaHangMucForUser Error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Kiểm tra file có thể xem trực tiếp không
        /// </summary>
        private bool CanViewInline(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            extension = extension.ToLower();

            var supportedExtensions = new[] { "pdf", "jpg", "jpeg", "png", "gif", "bmp", "doc", "docx", "xls", "xlsx" };

            return supportedExtensions.Contains(extension);
        }

        /// <summary>
        /// Kiểm tra file có phải là image không
        /// </summary>
        private bool IsImage(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            extension = extension.ToLower();
            var imageExtensions = new[] { "jpg", "jpeg", "png", "gif", "bmp" };

            return imageExtensions.Contains(extension);
        }

        private string GetContentType(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return "application/octet-stream";

            extension = extension.TrimStart('.').ToLower();

            switch (extension)
            {
                case "pdf":
                    return "application/pdf";

                case "jpg":
                case "jpeg":
                    return "image/jpeg";

                case "png":
                    return "image/png";

                case "gif":
                    return "image/gif";

                case "bmp":
                    return "image/bmp";

                case "doc":
                    return "application/msword";

                case "docx":
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

                case "xls":
                    return "application/vnd.ms-excel";

                case "xlsx":
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                case "txt":
                    return "text/plain";

                case "zip":
                    return "application/zip";

                case "rar":
                    return "application/x-rar-compressed";

                default:
                    return "application/octet-stream";
            }
        }

        /// <summary>
        /// POST: Budget/UpdateWorkflowType - Cập nhật loại luồng quy trình
        /// Chỉ Admin/Manager được phép
        /// </summary>
        [HttpPost]
        public JsonResult UpdateWorkflowType(int budgetId, int workflowType)
        {
            try
            {
                // Validate current user
                if (CurrentUser == null)
                {
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
                }

                // Check permissions - Only Admin/Manager can update workflow
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;

                if (!isManagerOrAdmin)
                {
                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
                }

                // Validate budgetId
                if (budgetId <= 0)
                {
                    return Json(new { success = false, message = "Mã đăng ký không hợp lệ." });
                }

                // Validate workflowType
                if (!Enum.IsDefined(typeof(WorkflowType), workflowType))
                {
                    return Json(new { success = false, message = "Loại luồng quy trình không hợp lệ." });
                }

                // Get budget registration
                var budgetRegistration = db.BudgetRegistrations.FirstOrDefault(x => x.BudgetRegistrationId == budgetId);

                if (budgetRegistration == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ đăng ký." });
                }

                // Update workflow type
                budgetRegistration.WorkflowType = workflowType;
                budgetRegistration.UpdatedDate = DateTime.Now;
                //budgetRegistration.UpdatedBy = CurrentUser.UserId;

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Cập nhật luồng quy trình thành công.",
                    workflowType = workflowType
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateWorkflowType Error: {ex.Message}");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi cập nhật. Vui lòng thử lại." });
            }
        }

        // ================================================================
        // GET: /Budget/GetWorkItems
        // ================================================================
        [HttpGet]
        public JsonResult GetWorkItems()
        {
            var items = db.WorkItems
                          .Where(w => w.IsActive)
                          .OrderBy(w => w.SortOrder)
                          .Select(w => new
                          {
                              w.WorkItemId,
                              w.Code,
                              w.Name
                          })
                          .ToList();

            return Json(items, JsonRequestBehavior.AllowGet);
        }

        // ================================================================
        // GET: /Budget/GetProgressLog?budgetId=xxx
        // ================================================================
        [HttpGet]
        public JsonResult GetProgressLog(int budgetId)
        {
            var log = db.ProgressLogs
                        .FirstOrDefault(p => p.BudgetRegistrationId == budgetId);

            if (log == null)
                return Json(new { exists = false }, JsonRequestBehavior.AllowGet);

            // Bước 1: Lấy dữ liệu thô từ DB (không format ngày ở đây)
            var areasRaw = db.ProgressLogAreas
                             .Where(a => a.ProgressLogId == log.ProgressLogId)
                             .OrderBy(a => a.SortOrder)
                             .Select(a => new
                             {
                                 a.ProgressLogAreaId,
                                 a.TenKhuVuc,
                                 a.TienDo,
                                 Items = db.ProgressItems
                                           .Where(i => i.ProgressLogAreaId == a.ProgressLogAreaId)
                                           .OrderBy(i => i.SortOrder)
                                           .Select(i => new
                                           {
                                               i.ProgressItemId,
                                               i.WorkItemId,
                                               i.TenHangMuc,
                                               i.DonViTinh,
                                               i.KLKeHoach,
                                               i.KLThucTe,
                                               i.PhanTram,
                                               i.NgayBatDauKH,   // Lấy DateTime? thô
                                               i.NgayKetThucKH,  // Lấy DateTime? thô
                                               i.GhiChu
                                           })
                                           .ToList()
                             })
                             .ToList();

            // Bước 2: Format ngày trên C# (sau khi đã rời khỏi EF query)
            var areas = areasRaw.Select(a => new
            {
                a.ProgressLogAreaId,
                a.TenKhuVuc,
                a.TienDo,
                Items = a.Items.Select(i => new
                {
                    i.ProgressItemId,
                    i.WorkItemId,
                    i.TenHangMuc,
                    i.DonViTinh,
                    i.KLKeHoach,
                    i.KLThucTe,
                    i.PhanTram,
                    NgayBatDauKH = i.NgayBatDauKH.HasValue
                        ? i.NgayBatDauKH.Value.ToString("yyyy-MM-dd") : null,
                    NgayKetThucKH = i.NgayKetThucKH.HasValue
                        ? i.NgayKetThucKH.Value.ToString("yyyy-MM-dd") : null,
                    i.GhiChu
                }).ToList()
            }).ToList();

            return Json(new
            {
                exists = true,
                log.ProgressLogId,
                log.DanhGiaChung,
                log.LuuY,
                log.TongTienDo,
                Areas = areas
            }, JsonRequestBehavior.AllowGet);
        }

        // ================================================================
        // POST: /Budget/SaveProgressLog
        // ================================================================
        [HttpPost]
        public JsonResult SaveProgressLog(SaveProgressLogDto dto)
        {
            if (dto == null || dto.BudgetRegistrationId <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            try
            {
                _SaveProgressLog(dto);  // dùng lại private method
                return Json(new { success = true, message = "Lưu nhật ký tiến độ thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        //private void SaveProgressData(int budgetRegistrationId,
        //                       ProgressConfigViewModel model,
        //                       int? createdByUserId = null)
        //{
        //    if (model == null) return;

        //    // Lưu ProgressConfig (upsert) — giữ nguyên
        //    var existingConfig = db.ProgressConfigs
        //        .FirstOrDefault(x => x.BudgetRegistrationId
        //                           == budgetRegistrationId);
        //    if (existingConfig == null)
        //    {
        //        existingConfig = new ProgressConfig
        //        {
        //            BudgetRegistrationId = budgetRegistrationId
        //        };
        //        db.ProgressConfigs.Add(existingConfig);
        //    }
        //    existingConfig.TiTrongXayDung = model.TiTrongXayDung;
        //    existingConfig.TiTrongKetCauThep = model.TiTrongKetCauThep;
        //    existingConfig.TiTrongLapDatThietBi = model.TiTrongLapDatThietBi;
        //    existingConfig.TiTrongHangMucKhac = model.TiTrongHangMucKhac;
        //    existingConfig.DanhGiaChung = model.DanhGiaChung;
        //    existingConfig.TongTienDo = model.TongTienDo;

        //    // *** THAY ĐỔI: Phân nhánh theo quyền ***
        //    bool isAdminOrManager =
        //        CurrentUser.RoleId == Constants.RoleConst.Admin ||
        //        CurrentUser.RoleId == Constants.RoleConst.Manager;

        //    if (isAdminOrManager)
        //    {
        //        // 1. Xóa sạch để chuẩn bị insert lại (giữ nguyên logic xóa của Admin)
        //        var oldAreasAll = db.ProgressAreas
        //            .Include(a => a.ProgressAreaItems)
        //            .Where(x => x.BudgetRegistrationId == budgetRegistrationId)
        //            .ToList();
        //        foreach (var area in oldAreasAll)
        //            db.ProgressAreaItems.RemoveRange(area.ProgressAreaItems);
        //        db.ProgressAreas.RemoveRange(oldAreasAll);
        //        db.SaveChanges();

        //        if (model.DanhSachKhuVuc != null && model.DanhSachKhuVuc.Any())
        //        {
        //            int areaOrder = 1;
        //            foreach (var khuVuc in model.DanhSachKhuVuc)
        //            {
        //                if (string.IsNullOrWhiteSpace(khuVuc.TenKhuVuc)) continue;

        //                // THAY ĐỔI: Xác định chủ sở hữu khu vực
        //                // Nếu khu vực cũ có ID người tạo -> giữ lại. Nếu mới (null) -> gán Admin hiện tại.
        //                int? areaOwnerId = khuVuc.CreatedByUserId ?? createdByUserId;

        //                var newArea = new ProgressArea
        //                {
        //                    BudgetRegistrationId = budgetRegistrationId,
        //                    TenKhuVuc = khuVuc.TenKhuVuc.Trim(),
        //                    SortOrder = areaOrder++,
        //                    CreatedByUserId = areaOwnerId
        //                };
        //                db.ProgressAreas.Add(newArea);
        //                db.SaveChanges();

        //                if (khuVuc.DanhSachDong != null && khuVuc.DanhSachDong.Any())
        //                {
        //                    int itemOrder = 1;
        //                    foreach (var dong in khuVuc.DanhSachDong)
        //                    {
        //                        // THAY ĐỔI QUAN TRỌNG: Xác định chủ sở hữu dòng (Item)
        //                        // Nếu dòng cũ có ID người tạo -> giữ lại. Nếu mới (null) -> gán Admin hiện tại.
        //                        int? itemOwnerId = dong.CreatedByUserId ?? createdByUserId;

        //                        db.ProgressAreaItems.Add(new ProgressAreaItem
        //                        {
        //                            ProgressAreaId = newArea.ProgressAreaId,
        //                            HangMucCongViec = dong.HangMucCongViec,
        //                            HangMucNhapTay = dong.HangMucNhapTay?.Trim(),
        //                            MoTaHangMuc = dong.MoTaHangMuc?.Trim(),
        //                            YKienPDA = dong.YKienPDA?.Trim(),
        //                            DVT = dong.DVT?.Trim(),
        //                            KLHD = dong.KLHD,
        //                            KLTT = dong.KLTT,
        //                            GhiChu = dong.GhiChu?.Trim(),
        //                            SortOrder = itemOrder++,
        //                            CreatedByUserId = itemOwnerId // Đã bảo toàn được chủ sở hữu gốc
        //                        });
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // User thường/chủ hồ sơ/phân nhiệm:
        //        // Chỉ xóa + insert khu vực của chính mình — giữ nguyên logic cũ
        //        var oldAreas = db.ProgressAreas
        //            .Include(a => a.ProgressAreaItems)
        //            .Where(x => x.BudgetRegistrationId == budgetRegistrationId
        //                     && x.CreatedByUserId == createdByUserId)
        //            .ToList();
        //        foreach (var area in oldAreas)
        //            db.ProgressAreaItems.RemoveRange(area.ProgressAreaItems);
        //        db.ProgressAreas.RemoveRange(oldAreas);

        //        if (model.DanhSachKhuVuc != null && model.DanhSachKhuVuc.Any())
        //        {
        //            int areaOrder = db.ProgressAreas
        //                .Where(x => x.BudgetRegistrationId == budgetRegistrationId)
        //                .Select(x => (int?)x.SortOrder)
        //                .Max() ?? 0;

        //            foreach (var khuVuc in model.DanhSachKhuVuc)
        //            {
        //                if (string.IsNullOrWhiteSpace(khuVuc.TenKhuVuc)) continue;

        //                if (khuVuc.CreatedByUserId == createdByUserId || khuVuc.CreatedByUserId == null)
        //                {
        //                    // ── Khu vực của chính user: xóa + insert lại như cũ ──
        //                    // (các khu vực này đã bị xóa ở bước trên cùng với toàn bộ item của nó)
        //                    var newArea = new ProgressArea
        //                    {
        //                        BudgetRegistrationId = budgetRegistrationId,
        //                        TenKhuVuc = khuVuc.TenKhuVuc.Trim(),
        //                        SortOrder = ++areaOrder,
        //                        CreatedByUserId = createdByUserId
        //                    };
        //                    db.ProgressAreas.Add(newArea);
        //                    db.SaveChanges();

        //                    if (khuVuc.DanhSachDong != null && khuVuc.DanhSachDong.Any())
        //                    {
        //                        int itemOrder = 1;
        //                        foreach (var dong in khuVuc.DanhSachDong)
        //                        {
        //                            // Xác định User ID cho dòng này:
        //                            // Nếu là dòng của mình hoặc dòng mới (null) -> gán ID của mình.
        //                            // Nếu là dòng của người khác -> phải giữ nguyên ID của họ (không được chiếm hữu).
        //                            int? rowOwnerId = (dong.CreatedByUserId == null || dong.CreatedByUserId == createdByUserId)
        //                                              ? createdByUserId
        //                                              : dong.CreatedByUserId;

        //                            db.ProgressAreaItems.Add(new ProgressAreaItem
        //                            {
        //                                ProgressAreaId = newArea.ProgressAreaId,
        //                                HangMucCongViec = dong.HangMucCongViec,
        //                                HangMucNhapTay = dong.HangMucNhapTay?.Trim(),
        //                                MoTaHangMuc = dong.MoTaHangMuc?.Trim(),
        //                                YKienPDA = dong.YKienPDA?.Trim(),
        //                                DVT = dong.DVT?.Trim(),
        //                                KLHD = dong.KLHD,
        //                                KLTT = dong.KLTT,
        //                                GhiChu = dong.GhiChu?.Trim(),
        //                                SortOrder = itemOrder++,
        //                                CreatedByUserId = rowOwnerId // CẬP NHẬT: Bảo tồn chủ sở hữu dòng
        //                            });
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    // ── Khu vực của người khác ───────────────────────────────
        //                    var existingArea = db.ProgressAreas
        //                        .FirstOrDefault(a =>
        //                            a.BudgetRegistrationId == budgetRegistrationId
        //                         && a.TenKhuVuc == khuVuc.TenKhuVuc.Trim()
        //                         && a.CreatedByUserId == khuVuc.CreatedByUserId);

        //                    if (existingArea == null) continue;

        //                    // *** THAY ĐỔI: Xóa các dòng của user hiện tại trong khu vực này
        //                    // rồi insert lại — tránh bỏ sót update/delete ***
        //                    var oldItemsOfCurrentUser = db.ProgressAreaItems
        //                        .Where(i => i.ProgressAreaId == existingArea.ProgressAreaId
        //                                 && i.CreatedByUserId == createdByUserId)
        //                        .ToList();
        //                    db.ProgressAreaItems.RemoveRange(oldItemsOfCurrentUser);
        //                    db.SaveChanges();

        //                    if (khuVuc.DanhSachDong != null && khuVuc.DanhSachDong.Any())
        //                    {
        //                        // Lấy SortOrder lớn nhất hiện có (của người khác)
        //                        int maxItemOrder = db.ProgressAreaItems
        //                            .Where(i => i.ProgressAreaId == existingArea.ProgressAreaId)
        //                            .Select(i => (int?)i.SortOrder)
        //                            .Max() ?? 0;

        //                        foreach (var dong in khuVuc.DanhSachDong)
        //                        {
        //                            // *** Insert tất cả dòng của user hiện tại
        //                            // (cả dòng mới lẫn dòng cũ đã sửa) ***
        //                            if (dong.CreatedByUserId != createdByUserId
        //                             && dong.CreatedByUserId != null) continue;

        //                            db.ProgressAreaItems.Add(new ProgressAreaItem
        //                            {
        //                                ProgressAreaId = existingArea.ProgressAreaId,
        //                                HangMucCongViec = dong.HangMucCongViec,
        //                                HangMucNhapTay = dong.HangMucNhapTay?.Trim(),
        //                                MoTaHangMuc = dong.MoTaHangMuc?.Trim(),
        //                                YKienPDA = dong.YKienPDA?.Trim(),
        //                                DVT = dong.DVT?.Trim(),
        //                                KLHD = dong.KLHD,
        //                                KLTT = dong.KLTT,
        //                                GhiChu = dong.GhiChu?.Trim(),
        //                                SortOrder = ++maxItemOrder,
        //                                CreatedByUserId = createdByUserId
        //                            });
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// GET: Budget/GetBudgetApprovals - Lấy danh sách phê duyệt ngân sách
        /// </summary>
        //[HttpGet]
        //public JsonResult GetBudgetApprovals(int budgetId)
        //{
        //    try
        //    {
        //        // Validate budgetId
        //        if (budgetId <= 0)
        //        {
        //            return Json(new { success = false, message = "Mã đăng ký không hợp lệ." }, JsonRequestBehavior.AllowGet);
        //        }

        //        // Validate current user
        //        if (CurrentUser == null)
        //        {
        //            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." }, JsonRequestBehavior.AllowGet);
        //        }

        //        // Get budget registration
        //        var budgetRegistration = db.BudgetRegistrations
        //            .FirstOrDefault(x => x.BudgetRegistrationId == budgetId);

        //        if (budgetRegistration == null)
        //        {
        //            return Json(new { success = false, message = "Không tìm thấy hồ sơ đăng ký." }, JsonRequestBehavior.AllowGet);
        //        }

        //        // Check permissions
        //        bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
        //                                CurrentUser.RoleId == Constants.RoleConst.Manager;

        //        if (!isManagerOrAdmin && budgetRegistration.UserId != CurrentUser.UserId)
        //        {
        //            return Json(new { success = false, message = "Bạn không có quyền xem thông tin này." }, JsonRequestBehavior.AllowGet);
        //        }

        //        // Get approvals list
        //        var approvals = db.BudgetApprovals
        //            .Where(x => x.BudgetRegistrationId == budgetId && !x.IsDeleted)
        //            .OrderByDescending(x => x.CreatedDate)
        //            .Select(x => new BudgetApprovalListViewModel
        //            {
        //                BudgetApprovalId = x.BudgetApprovalId,
        //                ApprovalProcessType = x.ApprovalProcessType,
        //                ApprovalProcessTypeName = x.ApprovalProcessType == 1 ? "Công ty" : "Tập đoàn",
        //                ApprovedAmount = x.ApprovedAmount,
        //                NotificationNumber = x.NotificationNumber,
        //                FmIoNumber = x.FmIoNumber,
        //                PhongDuAnDate = x.PhongDuAnDate,
        //                PhongKeToanDate = x.PhongKeToanDate,
        //                ERPDDate = x.ERPDDate,
        //                BanTaiChinhDate = x.BanTaiChinhDate,
        //                BanGiamDocDate = x.BanGiamDocDate,
        //                CreatedDate = x.CreatedDate,
        //                CreatedByName = x.User.HoTen ?? "N/A"
        //            })
        //            .ToList();

        //        var result = new BudgetApprovalsDataViewModel
        //        {
        //            Approvals = approvals,
        //            OriginalBudget = budgetRegistration.DuToan ?? 0,
        //            CanEdit = isManagerOrAdmin
        //        };

        //        return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"GetBudgetApprovals Error: {ex.Message}");
        //        return Json(new { success = false, message = "Đã xảy ra lỗi khi tải dữ liệu." }, JsonRequestBehavior.AllowGet);
        //    }
        //}

        /// <summary>
        /// POST: Budget/SaveBudgetApproval - Lưu phê duyệt ngân sách
        /// </summary>
        //[HttpPost]
        //public JsonResult SaveBudgetApproval(BudgetApprovalFormViewModel model)
        //{
        //    try
        //    {
        //        // Validate current user
        //        if (CurrentUser == null)
        //        {
        //            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        //        }

        //        // Check permissions - Only Admin/Manager
        //        bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
        //                                CurrentUser.RoleId == Constants.RoleConst.Manager;

        //        if (!isManagerOrAdmin)
        //        {
        //            return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
        //        }

        //        // Validate model
        //        if (!ModelState.IsValid)
        //        {
        //            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
        //            return Json(new { success = false, message = string.Join(", ", errors) });
        //        }

        //        // Validate: Must have at least one date
        //        if (!model.PhongDuAnDate.HasValue && !model.PhongKeToanDate.HasValue &&
        //            !model.ERPDDate.HasValue && !model.BanTaiChinhDate.HasValue && !model.BanGiamDocDate.HasValue)
        //        {
        //            return Json(new { success = false, message = "Vui lòng nhập ít nhất một ngày mốc." });
        //        }

        //        // Validate: Notification and FM/IO required if BGD date exists
        //        if (model.BanGiamDocDate.HasValue)
        //        {
        //            if (string.IsNullOrWhiteSpace(model.NotificationNumber))
        //            {
        //                return Json(new { success = false, message = "Vui lòng nhập số thông báo khi đã có ngày duyệt BGĐ." });
        //            }
        //            if (string.IsNullOrWhiteSpace(model.FmIoNumber))
        //            {
        //                return Json(new { success = false, message = "Vui lòng nhập số FM/IO khi đã có ngày duyệt BGĐ." });
        //            }
        //        }

        //        // Validate: Date sequence
        //        var validationResult = ValidateDateSequence(model);
        //        if (!validationResult.IsValid)
        //        {
        //            return Json(new { success = false, message = validationResult.ErrorMessage });
        //        }

        //        BudgetApproval entity;

        //        if (model.BudgetApprovalId.HasValue && model.BudgetApprovalId.Value > 0)
        //        {
        //            // Update existing
        //            entity = db.BudgetApprovals.FirstOrDefault(x => x.BudgetApprovalId == model.BudgetApprovalId.Value);

        //            if (entity == null)
        //            {
        //                return Json(new { success = false, message = "Không tìm thấy phê duyệt cần cập nhật." });
        //            }

        //            entity.ApprovalProcessType = model.ApprovalProcessType;
        //            entity.ApprovedAmount = model.ApprovedAmount;
        //            entity.NotificationNumber = model.NotificationNumber?.Trim();
        //            entity.FmIoNumber = model.FmIoNumber?.Trim();
        //            entity.PhongDuAnDate = model.PhongDuAnDate;
        //            entity.PhongKeToanDate = model.PhongKeToanDate;
        //            entity.ERPDDate = model.ERPDDate;
        //            entity.BanTaiChinhDate = model.BanTaiChinhDate;
        //            entity.BanGiamDocDate = model.BanGiamDocDate;
        //            entity.UpdatedDate = DateTime.Now;
        //            entity.UpdatedBy = CurrentUser.UserId;
        //        }
        //        else
        //        {
        //            // Create new
        //            entity = new BudgetApproval
        //            {
        //                BudgetRegistrationId = model.BudgetRegistrationId,
        //                ApprovalProcessType = model.ApprovalProcessType,
        //                ApprovedAmount = model.ApprovedAmount,
        //                NotificationNumber = model.NotificationNumber?.Trim(),
        //                FmIoNumber = model.FmIoNumber?.Trim(),
        //                PhongDuAnDate = model.PhongDuAnDate,
        //                PhongKeToanDate = model.PhongKeToanDate,
        //                ERPDDate = model.ERPDDate,
        //                BanTaiChinhDate = model.BanTaiChinhDate,
        //                BanGiamDocDate = model.BanGiamDocDate,
        //                CreatedDate = DateTime.Now,
        //                CreatedBy = CurrentUser.UserId,
        //                IsDeleted = false
        //            };

        //            db.BudgetApprovals.Add(entity);
        //        }

        //        db.SaveChanges();

        //        return Json(new
        //        {
        //            success = true,
        //            message = "Lưu phê duyệt thành công.",
        //            approvalId = entity.BudgetApprovalId
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"SaveBudgetApproval Error: {ex.Message}");
        //        return Json(new { success = false, message = "Đã xảy ra lỗi khi lưu dữ liệu." });
        //    }
        //}

        /// <summary>
        /// POST: Budget/DeleteBudgetApproval - Xóa phê duyệt
        /// </summary>
        //[HttpPost]
        //public JsonResult DeleteBudgetApproval(int approvalId)
        //{
        //    try
        //    {
        //        // Validate user
        //        if (CurrentUser == null)
        //        {
        //            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        //        }

        //        // Check permissions
        //        bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
        //                                CurrentUser.RoleId == Constants.RoleConst.Manager;

        //        if (!isManagerOrAdmin)
        //        {
        //            return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
        //        }

        //        var approval = db.BudgetApprovals.FirstOrDefault(x => x.BudgetApprovalId == approvalId);

        //        if (approval == null)
        //        {
        //            return Json(new { success = false, message = "Không tìm thấy phê duyệt cần xóa." });
        //        }

        //        // Soft delete
        //        approval.IsDeleted = true;
        //        approval.UpdatedDate = DateTime.Now;
        //        approval.UpdatedBy = CurrentUser.UserId;

        //        db.SaveChanges();

        //        return Json(new { success = true, message = "Xóa phê duyệt thành công." });
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"DeleteBudgetApproval Error: {ex.Message}");
        //        return Json(new { success = false, message = "Đã xảy ra lỗi khi xóa." });
        //    }
        //}

        /// <summary>
        /// Validate date sequence - mốc sau phải sau mốc trước
        /// </summary>
        private (bool IsValid, string ErrorMessage) ValidateDateSequence(BudgetApprovalFormViewModel model)
        {
            var dates = new List<(string Name, DateTime? Date)>();

            dates.Add(("Phòng dự án", model.PhongDuAnDate));
            dates.Add(("Phòng kế toán", model.PhongKeToanDate));

            if (model.ApprovalProcessType == 2) // Tập đoàn
            {
                dates.Add(("ERPD", model.ERPDDate));
                dates.Add(("Ban tài chính", model.BanTaiChinhDate));
            }

            dates.Add(("BGĐ", model.BanGiamDocDate));

            // Get only dates that have values
            var filledDates = dates.Where(x => x.Date.HasValue).ToList();

            for (int i = 0; i < filledDates.Count - 1; i++)
            {
                if (filledDates[i].Date.Value > filledDates[i + 1].Date.Value)
                {
                    return (false, $"Ngày {filledDates[i + 1].Name} phải sau ngày {filledDates[i].Name}.");
                }
            }

            return (true, string.Empty);
        }

        // HELPER METHODS — thêm mới
        // ============================================================
        private IEnumerable<SelectListItem> GetPhongBanSelectList()
        {
            return db.PhongBans
                .OrderBy(p => p.TenPhongBan)
                .Select(p => new SelectListItem
                {
                    Value = p.PhongBanId.ToString(),
                    Text = p.TenPhongBan
                })
                .ToList();
        }

        private IEnumerable<SelectListItem> GetChucNangSelectList()
        {
            return db.ChucNang_NhiemVu
                .OrderBy(c => c.TenChucNang)
                .Select(c => new SelectListItem
                {
                    Value = c.ChucNangNhiemVuId.ToString(),
                    Text = c.TenChucNang
                })
                .ToList();
        }

        private IEnumerable<SelectListItem> GetProjectAreaSelectList()
        {
            return db.ProjectAreas
                .OrderBy(p => p.AreaName)
                .Select(p => new SelectListItem
                {
                    Value = p.ProjectAreaId.ToString(),
                    Text = p.AreaName
                })
                .ToList();
        }
    }
}