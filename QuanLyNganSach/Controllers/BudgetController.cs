using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
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

        // GET: Budget
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Create()
        {
            var model = new CreateBudgetRegistrationViewModel
            {
                MaHangMuc = GenerateMaHangMuc(),
                CategoryTypes = GetCategoryTypes(),
                PriorityLevels = GetPriorityLevels(),
                NgayBatDau = DateTime.Today,
                NgayKetThuc = DateTime.Today
            };

            ViewBag.TenBoPhan = CurrentUser.TenPhongBan;

            return View(model);
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

        private string GenerateMaHangMuc()
        {
            if (CurrentUser == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(CurrentUser.MaPhongBan))
                return string.Empty;

            if (CurrentUser.PhongBanId <= 0)
                return string.Empty;

            int sequence = 1;

            try
            {
                sequence = db.BudgetRegistrations
                             .Count(x => x.PhongBanId == CurrentUser.PhongBanId) + 1;
            }
            catch
            {
                // fallback
                sequence = 1;
            }

            // Format sequence
            string sequenceFormatted = sequence.ToString("D2");

            string currentDate = DateTime.Now.ToString("dd.MM.yyyy");

            return $"{CurrentUser.MaPhongBan}.{sequenceFormatted}.{currentDate}";
        }
    }
}