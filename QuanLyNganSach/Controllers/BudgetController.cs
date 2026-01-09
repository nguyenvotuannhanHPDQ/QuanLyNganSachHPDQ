using PagedList;
using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QuanLyNganSach.Controllers
{
    public class BudgetController : BaseController
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();
        private const int PageSize = 10;

        // GET: Budget/Index - Danh sách đăng ký ngân sách
        public ActionResult Index(int? page, string search, string sortOrder, int? phongBanId)
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
                    DuToan = x.BudgetRegistration.DuToan,
                    SoToTrinh = x.BudgetRegistration.SoToTrinh,
                    LyDoDauTu = x.BudgetRegistration.LyDoDauTu,
                    MoTaKyThuat = x.BudgetRegistration.MoTaKyThuat,
                    NgayBatDau = x.BudgetRegistration.NgayBatDau,
                    NgayKetThuc = x.BudgetRegistration.NgayKetThuc,
                    TenPhongBan = x.PhongBan.TenPhongBan,
                    PhongBanId = x.PhongBan.PhongBanId,
                    NguoiDangKy = x.User.HoTen,
                    NgayTao = x.BudgetRegistration.CreatedDate,
                    //TrangThai = x.BudgetRegistration.TrangThai
                });

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

                    // Load dropdown lists
                    CategoryTypes = GetCategoryTypes(),
                    PriorityLevels = GetPriorityLevels(),

                    // Set default dates
                    NgayBatDau = DateTime.Today,
                    NgayKetThuc = DateTime.Today,

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
                ModelState.AddModelError("", "Ngày kết thúc phải >= ngày bắt đầu");
                ReloadDropdowns(model);
                return View(model);
            }

            var entity = new BudgetRegistration
            {
                MaHangMuc = model.MaHangMuc,
                TenHangMuc = model.TenHangMuc,
                DuToan = model.DuToan,
                SoToTrinh = model.SoToTrinh,
                CategoryTypeId = model.CategoryTypeId,
                PriorityLevelId = model.PriorityLevelId,
                SoLuong = model.SoLuong,
                LyDoDauTu = model.LyDoDauTu,
                MoTaKyThuat = model.MoTaKyThuat,
                NgayBatDau = model.NgayBatDau,
                NgayKetThuc = model.NgayKetThuc,
                UserId = CurrentUser.UserId,
                PhongBanId = (int) CurrentUser.PhongBanId,
                CreatedDate = DateTime.Now
            };

            db.BudgetRegistrations.Add(entity);
            db.SaveChanges();

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
                             //&& x.CreatedDate >= startOfMonth && x.CreatedDate <= endOfMonth
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
    }
}