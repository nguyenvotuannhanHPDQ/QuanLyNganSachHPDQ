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

namespace QuanLyNganSach.Controllers
{
    public class BudgetController : BaseController
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();
        private const int PageSize = 10;

        // GET: Budget/Index - Danh sách đăng ký ngân sách
        public ActionResult Index(int? page, string search, string sortOrder, int? phongBanId,int? filterTienDo, int? filterTrangThai)
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
                if (!isManagerOrAdmin)
                {
                    // User thường chỉ xem được đăng ký của mình
                    query = query.Where(x => x.BudgetRegistration.UserId == currentUser.UserId);
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

        public ActionResult Create()
        {
            try
            {
                // Create view model with default values
                var model = new CreateBudgetRegistrationViewModel
                {
                    // Generate MaHangMuc with default CategoryTypeId = 1
                    MaHangMuc = GenerateMaHangMuc(1),

                    // Set default CategoryTypeId
                    CategoryTypeId = 1,
                    SoLuong = 1,

                    // Load dropdown lists
                    CategoryTypes = GetCategoryTypes(),
                    PriorityLevels = GetPriorityLevels(),

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateBudgetRegistrationViewModel model)
        {
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

            // Create entity from view model
            var entity = new BudgetRegistration
            {
                MaHangMuc = model.MaHangMuc,
                TenHangMuc = model.TenHangMuc?.Trim(),
                DuToan = model.DuToan,
                SoToTrinh = model.SoToTrinh?.Trim(),
                SoLuong = model.SoLuong,
                CategoryTypeId = model.CategoryTypeId,
                PriorityLevelId = model.PriorityLevelId,
                LyDoDauTu = model.LyDoDauTu?.Trim(),
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
                    ThieuEmail = string.IsNullOrEmpty(p.Email?.Trim())
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
                        GhiChu = pn.GhiChu?.Trim()
                    };

                    db.BudgetRegistrationPhanNhiems.Add(phanNhiem);
                }

                db.SaveChanges(); // SaveChanges lần 2 cho Phân nhiệm
            }

            if (model.HoSoCanCu?.ContentLength > 0)
            {
                SaveHoSoCanCuAndAttachment(model.HoSoCanCu, entity.BudgetRegistrationId);
            }

            TempData["Success"] = "Đăng ký hồ sơ ngân sách thành công";
            return RedirectToAction("Create");
        }

        private void SaveHoSoCanCuAndAttachment(HttpPostedFileBase file, int budgetRegistrationId)
        {
            if (file == null || file.ContentLength <= 0)
                return;

            var uploadFolder = Server.MapPath("~/Uploads/HoSoCanCu");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var fileExtension = Path.GetExtension(file.FileName);
            var originalFileName = Path.GetFileName(file.FileName);
            var safeFileName = $"{Guid.NewGuid():N}{fileExtension}";
            var fullPath = Path.Combine(uploadFolder, safeFileName);

            try
            {
                file.SaveAs(fullPath);

                var attachment = new BudgetAttachment
                {
                    BudgetRegistrationId = budgetRegistrationId,
                    FileName = originalFileName,
                    FilePath = "/Uploads/HoSoCanCu/" + safeFileName,
                    FileExtension = fileExtension,
                    FileSize = file.ContentLength,
                    UploadedBy = CurrentUser.UserId,
                    UploadedDate = DateTime.Now
                };

                db.BudgetAttachments.Add(attachment);
                db.SaveChanges();
            }
            catch
            {
                
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
                .Include(x => x.BudgetRegistrationPhanNhiems
                               .Select(p => p.PhongBan))
                .Include(x => x.BudgetRegistrationPhanNhiems
                               .Select(p => p.ChucNang_NhiemVu))
                .Include(x => x.BudgetApprovals)
                .Include(x => x.ProgressConfigs)
                .Include(x => x.ProgressAreas.Select(a => a.ProgressAreaItems))
                .FirstOrDefault(x => x.BudgetRegistrationId == id.Value);

                // Check if record exists
                if (budgetRegistration == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin đăng ký." }, JsonRequestBehavior.AllowGet);
                }

                // Check permissions
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;

                if (!isManagerOrAdmin && budgetRegistration.UserId != CurrentUser.UserId)
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

                    //CategoryTypeId = budgetRegistration.CategoryTypeId,
                    CategoryTypeName = budgetRegistration.BudgetCategoryType?.CategoryTypeName ?? "N/A",
                    //PriorityLevelId = budgetRegistration.PriorityLevelId,
                    PriorityLevelName = budgetRegistration.BudgetPriorityLevel?.PriorityLevelName ?? "N/A",

                    NgayBatDau = budgetRegistration.NgayBatDau,
                    NgayKetThuc = budgetRegistration.NgayKetThuc,
                    NgayTao = budgetRegistration.CreatedDate,
                    //NgayCapNhat = budgetRegistration.UpdatedDate,

                    //PhongBanId = budgetRegistration.PhongBanId,
                    TenPhongBan = budgetRegistration.PhongBan.TenPhongBan,
                    //MaPhongBan = budgetRegistration.PhongBan?.MaPhongBan ?? "N/A",

                    //UserId = budgetRegistration.UserId,
                    TenNguoiDangKy = budgetRegistration.User.HoTen,
                    //EmailNguoiDangKy = budgetRegistration.User?.Email,
                    //MaNhanVienDangKy = budgetRegistration.User?.MaNhanVien,

                    //TrangThai = budgetRegistration.TrangThai ?? "Chờ duyệt",

                    WorkflowType = budgetRegistration.WorkflowType,

                    IsManagerOrAdmin = isManagerOrAdmin,

                    // *** THÊM MỚI ***
                    //AreaName = budgetRegistration.ProjectArea?.AreaName ?? string.Empty,
                    ProjectAreaId = budgetRegistration.ProjectAreaId,

                    // *** THÊM MỚI: Phân nhiệm ***
                    DanhSachPhanNhiem = budgetRegistration.BudgetRegistrationPhanNhiems
                    .Select(p => new PhanNhiemViewModel
                    {
                        PhongBanId = p.PhongBanId,
                        TenPhongBan = p.PhongBan?.TenPhongBan,
                        ChucNangNhiemVuId = p.ChucNangNhiemVuId,
                        TenChucNang = p.ChucNang_NhiemVu?.TenChucNang,
                        TenChucNangNhapTay = p.TenChucNangNhapTay,
                        Email = p.Email,
                        GhiChu = p.GhiChu
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

                // Lấy tất cả BudgetApprovals của phiếu này
                var approvals = budgetRegistration.BudgetApprovals.ToList();
                var approvalGoc = approvals.FirstOrDefault(x => !x.IsSupplementary);
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
                    SoFMIO = approvalGoc?.SoFMIO,
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
                        SoFMIO = a.SoFMIO,
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
                    DanhSachKhuVuc = budgetRegistration.ProgressAreas
                        .OrderBy(a => a.SortOrder)
                        .Select(a => new ProgressAreaViewModel
                        {
                            ProgressAreaId = a.ProgressAreaId,
                            TenKhuVuc = a.TenKhuVuc,
                            SortOrder = a.SortOrder,
                            DanhSachDong = a.ProgressAreaItems
                                .OrderBy(i => i.SortOrder)
                                .Select(i => new ProgressAreaItemViewModel
                                {
                                    ProgressAreaItemId = i.ProgressAreaItemId,
                                    HangMucCongViec = i.HangMucCongViec,
                                    HangMucNhapTay = i.HangMucNhapTay,
                                    DVT = i.DVT,
                                    KLHD = i.KLHD,
                                    KLTT = i.KLTT,
                                    GhiChu = i.GhiChu,
                                    SortOrder = i.SortOrder
                                }).ToList()
                        }).ToList(),
                    TongTienDo = config?.TongTienDo
                };

                return Json(new { success = true, data = viewModel }, JsonRequestBehavior.AllowGet);
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
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });

                var errors = ModelState
                    .Where(x => x.Value.Errors.Any())
                    .Select(x => new {
                        Field = x.Key,
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage)
                });
                System.Diagnostics.Debug.WriteLine(
                    Newtonsoft.Json.JsonConvert.SerializeObject(errors));

                if (!ModelState.IsValid)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

                var entity = db.BudgetRegistrations
                    .FirstOrDefault(x => x.BudgetRegistrationId == model.BudgetRegistrationId);

                if (entity == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

                // Lưu WorkflowType
                entity.WorkflowType = model.WorkflowType;

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

                // Xóa toàn bộ BudgetApprovals cũ và insert lại (tương tự Phân nhiệm)
                var oldApprovals = db.BudgetApprovals
                    .Where(x => x.BudgetRegistrationId == model.BudgetRegistrationId)
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
                        SoFMIO = pd.SoFMIO?.Trim(),
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
                            SoFMIO = pd.SoFMIO?.Trim(),
                            TrangThaiPheDuyet = tinhTrangThai(pd),
                            IsSupplementary = true,
                            SupplementaryOrder = order++,
                            LyDoBoSung = pd.LyDoBoSung?.Trim(),
                            NganSachBoSung = pd.NganSachBoSung ?? 0
                        });
                    }
                }

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

                        db.BudgetRegistrationPhanNhiems.Add(new BudgetRegistrationPhanNhiem
                        {
                            BudgetRegistrationId = model.BudgetRegistrationId,
                            PhongBanId = pn.PhongBanId.Value,
                            ChucNangNhiemVuId = pn.ChucNangNhiemVuId,
                            TenChucNangNhapTay = pn.TenChucNangNhapTay?.Trim(),
                            Email = pn.Email?.Trim(),
                            GhiChu = pn.GhiChu?.Trim()
                        });
                    }
                }

                SaveProgressData(model.BudgetRegistrationId, model.ThongTinTienDo);

                db.SaveChanges();

                return Json(new { success = true, message = "Lưu thông tin thành công." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveDetailsModal Error: {ex.Message}");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi lưu thông tin." });
            }
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
        public ActionResult DownloadFile(int? id)
        {
            try
            {
                if (!id.HasValue || id.Value <= 0)
                {
                    TempData["Error"] = "Mã file không hợp lệ.";
                    return RedirectToAction("Index");
                }

                if (CurrentUser == null)
                {
                    TempData["Error"] = "Phiên đăng nhập đã hết hạn.";
                    return RedirectToAction("Login", "Account");
                }

                var attachment = db.BudgetAttachments
                    .Include(x => x.BudgetRegistration)
                    .FirstOrDefault(x => x.AttachmentId == id.Value);

                if (attachment == null)
                {
                    TempData["Error"] = "Không tìm thấy file đính kèm.";
                    return RedirectToAction("Index");
                }

                // Check permissions
                bool isManagerOrAdmin = CurrentUser.RoleId == Constants.RoleConst.Admin ||
                                        CurrentUser.RoleId == Constants.RoleConst.Manager;

                if (!isManagerOrAdmin && attachment.BudgetRegistration.UserId != CurrentUser.UserId)
                {
                    TempData["Error"] = "Bạn không có quyền tải file này.";
                    return RedirectToAction("Index");
                }

                // Build full file path
                string uploadsFolder = Server.MapPath("~/Uploads/HoSoCanCu");
                string fileName = Path.GetFileName(attachment.FilePath);
                string fullPath = Path.Combine(uploadsFolder, fileName);

                //// Check if file exists
                if (!System.IO.File.Exists(fullPath))
                {
                    TempData["Error"] = "File không tồn tại trên hệ thống.";
                    return RedirectToAction("Details", new { id = attachment.BudgetRegistrationId });
                }

                string extension = attachment.FileExtension?.TrimStart('.').ToLower() ?? string.Empty;
                string contentType = GetContentType(extension);

                byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);

                return File(fileBytes, contentType, attachment.FileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DownloadFile Error - ID: {id}, Message: {ex.Message}");
                TempData["Error"] = "Đã xảy ra lỗi khi tải file. Vui lòng thử lại.";
                return RedirectToAction("Index");
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
        private void SaveProgressData(int budgetRegistrationId,
                               ProgressConfigViewModel model)
        {
            if (model == null) return;

            // Lưu ProgressConfig (upsert)
            var existingConfig = db.ProgressConfigs
                .FirstOrDefault(x => x.BudgetRegistrationId == budgetRegistrationId);

            if (existingConfig == null)
            {
                existingConfig = new ProgressConfig
                {
                    BudgetRegistrationId = budgetRegistrationId
                };
                db.ProgressConfigs.Add(existingConfig);
            }

            existingConfig.TiTrongXayDung = model.TiTrongXayDung;
            existingConfig.TiTrongKetCauThep = model.TiTrongKetCauThep;
            existingConfig.TiTrongLapDatThietBi = model.TiTrongLapDatThietBi;
            existingConfig.TiTrongHangMucKhac = model.TiTrongHangMucKhac;
            existingConfig.DanhGiaChung = model.DanhGiaChung;
            existingConfig.TongTienDo = model.TongTienDo;

            // Xóa toàn bộ ProgressAreas + ProgressAreaItems cũ
            var oldAreas = db.ProgressAreas
                .Include(a => a.ProgressAreaItems)
                .Where(x => x.BudgetRegistrationId == budgetRegistrationId)
                .ToList();

            foreach (var area in oldAreas)
            {
                db.ProgressAreaItems.RemoveRange(area.ProgressAreaItems);
            }
            db.ProgressAreas.RemoveRange(oldAreas);

            // Insert ProgressAreas + ProgressAreaItems mới
            if (model.DanhSachKhuVuc != null && model.DanhSachKhuVuc.Any())
            {
                int areaOrder = 1;
                foreach (var khuVuc in model.DanhSachKhuVuc)
                {
                    if (string.IsNullOrWhiteSpace(khuVuc.TenKhuVuc)) continue;

                    var newArea = new ProgressArea
                    {
                        BudgetRegistrationId = budgetRegistrationId,
                        TenKhuVuc = khuVuc.TenKhuVuc.Trim(),
                        SortOrder = areaOrder++
                    };
                    db.ProgressAreas.Add(newArea);
                    db.SaveChanges(); // Lấy ProgressAreaId vừa insert

                    if (khuVuc.DanhSachDong != null && khuVuc.DanhSachDong.Any())
                    {
                        int itemOrder = 1;
                        foreach (var dong in khuVuc.DanhSachDong)
                        {
                            db.ProgressAreaItems.Add(new ProgressAreaItem
                            {
                                ProgressAreaId = newArea.ProgressAreaId,
                                HangMucCongViec = dong.HangMucCongViec,
                                HangMucNhapTay = dong.HangMucNhapTay?.Trim(),
                                DVT = dong.DVT?.Trim(),
                                KLHD = dong.KLHD,
                                KLTT = dong.KLTT,
                                GhiChu = dong.GhiChu?.Trim(),
                                SortOrder = itemOrder++
                            });
                        }
                    }
                }
            }
        }

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