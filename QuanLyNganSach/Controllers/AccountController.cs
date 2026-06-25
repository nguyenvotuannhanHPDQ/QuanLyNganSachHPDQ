using Newtonsoft.Json;
using QuanLyNganSach.Helpers;
using QuanLyNganSach.Models.Auth;
using QuanLyNganSach.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Configuration;
using OfficeOpenXml;
using ClosedXML.Excel;
using System.Data.Entity.Core.EntityClient;

namespace QuanLyNganSach.Controllers
{
    public class AccountController : BaseController
    {
        private readonly QuanLyNganSachEntities db = new QuanLyNganSachEntities();

        // GET: Users/ImportExcel
        public ActionResult ImportExcel()
        {
            return View();
        }

        // POST: Users/ImportExcel
        [HttpPost]
        public async Task<ActionResult> ImportExcel(HttpPostedFileBase file)
        {
            var result = await ImportUsersFromExcel(file);
            ViewBag.Message = result;
            return View();
        }

        public async Task<string> ImportUsersFromExcel(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return "File không hợp lệ";

            var dt = new DataTable();
            dt.Columns.Add("MaNhanVien", typeof(string));
            dt.Columns.Add("HoTen", typeof(string));
            dt.Columns.Add("Password", typeof(string));
            dt.Columns.Add("RoleId", typeof(int));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedDate", typeof(DateTime));
            dt.Columns.Add("PhongBanId", typeof(int));

            string passwordHash = HashSHA256("123456");

            using (var db = new QuanLyNganSachEntities())
            {
                var phongBanDict = db.PhongBans
                    .ToDictionary(x => x.TenPhongBan.Trim(), x => x.PhongBanId);

                var existingMaNV = new HashSet<string>(
                    db.Users.Select(x => x.MaNhanVien)
                );

                using (var workbook = new XLWorkbook(file.InputStream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed().Skip(1); // bỏ header

                    foreach (var row in rows)
                    {
                        string maNV = row.Cell(1).GetValue<string>()?.Trim();
                        string hoTen = row.Cell(2).GetValue<string>()?.Trim();
                        string tenPB = row.Cell(3).GetValue<string>()?.Trim();

                        if (string.IsNullOrEmpty(maNV)) continue;

                        if (existingMaNV.Contains(maNV)) continue;

                        int? phongBanId = null;
                        if (!string.IsNullOrEmpty(tenPB) && phongBanDict.ContainsKey(tenPB))
                        {
                            phongBanId = phongBanDict[tenPB];
                        }

                        dt.Rows.Add(
                            maNV,
                            hoTen,
                            passwordHash,
                            3,
                            false,
                            DateTime.Now,
                            phongBanId.HasValue ? (object)phongBanId.Value : DBNull.Value
                        );

                        existingMaNV.Add(maNV);
                    }
                }
            }

            // Bulk insert
            var entityConnStr = ConfigurationManager.ConnectionStrings["QuanLyNganSachEntities"].ConnectionString;

            // Tách ra connection string thật
            var entityBuilder = new EntityConnectionStringBuilder(entityConnStr);
            string sqlConnStr = entityBuilder.ProviderConnectionString;

            // Dùng cho SqlBulkCopy
            using (var conn = new SqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();

                using (var bulk = new SqlBulkCopy(conn))
                {
                    bulk.DestinationTableName = "dbo.Users";
                    bulk.BatchSize = 1000;
                    bulk.BulkCopyTimeout = 600;

                    bulk.ColumnMappings.Add("MaNhanVien", "MaNhanVien");
                    bulk.ColumnMappings.Add("HoTen", "HoTen");
                    bulk.ColumnMappings.Add("Password", "Password");
                    bulk.ColumnMappings.Add("RoleId", "RoleId");
                    bulk.ColumnMappings.Add("IsActive", "IsActive");
                    bulk.ColumnMappings.Add("CreatedDate", "CreatedDate");
                    bulk.ColumnMappings.Add("PhongBanId", "PhongBanId");

                    await bulk.WriteToServerAsync(dt);
                }
            }

            return $"Import thành công: {dt.Rows.Count} dòng";
        }

        public async Task<string> ImportUsersFromExcel_(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return "File không hợp lệ";

            var dt = new DataTable();
            dt.Columns.Add("MaNhanVien", typeof(string));
            dt.Columns.Add("HoTen", typeof(string));
            dt.Columns.Add("Password", typeof(string));
            dt.Columns.Add("RoleId", typeof(int));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedDate", typeof(DateTime));
            dt.Columns.Add("PhongBanId", typeof(int));

            string passwordHash = HashSHA256("123456");

            using (var db = new QuanLyNganSachEntities())
            {
                // Load sẵn dữ liệu để tránh query nhiều lần
                var phongBanDict = db.PhongBans
                    .ToDictionary(x => x.TenPhongBan.Trim(), x => x.PhongBanId);

                var existingMaNV = new HashSet<string>(
                    db.Users.Select(x => x.MaNhanVien)
                );
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using (var package = new OfficeOpenXml.ExcelPackage(file.InputStream))
                {
                    var ws = package.Workbook.Worksheets.First();
                    int rowCount = ws.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        string maNV = ws.Cells[row, 1].Text?.Trim();
                        string hoTen = ws.Cells[row, 2].Text?.Trim();
                        string tenPB = ws.Cells[row, 3].Text?.Trim();

                        if (string.IsNullOrEmpty(maNV)) continue;

                        // Bỏ qua nếu trùng
                        if (existingMaNV.Contains(maNV)) continue;

                        int? phongBanId = null;
                        if (!string.IsNullOrEmpty(tenPB) && phongBanDict.ContainsKey(tenPB))
                        {
                            phongBanId = phongBanDict[tenPB];
                        }

                        dt.Rows.Add(
                            maNV,
                            hoTen,
                            passwordHash,
                            3,
                            false,
                            DateTime.Now,
                            phongBanId.HasValue ? (object)phongBanId.Value : DBNull.Value
                        );

                        existingMaNV.Add(maNV);
                    }
                }
            }
            string connStr = ConfigurationManager.ConnectionStrings["QuanLyNganSachEntities"].ConnectionString;
            // BulkCopy
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                using (var bulk = new SqlBulkCopy(conn))
                {
                    bulk.DestinationTableName = "dbo.Users";
                    bulk.BatchSize = 1000;
                    bulk.BulkCopyTimeout = 600;

                    bulk.ColumnMappings.Add("MaNhanVien", "MaNhanVien");
                    bulk.ColumnMappings.Add("HoTen", "HoTen");
                    bulk.ColumnMappings.Add("Password", "Password");
                    bulk.ColumnMappings.Add("RoleId", "RoleId");
                    bulk.ColumnMappings.Add("IsActive", "IsActive");
                    bulk.ColumnMappings.Add("CreatedDate", "CreatedDate");
                    bulk.ColumnMappings.Add("PhongBanId", "PhongBanId");

                    await bulk.WriteToServerAsync(dt);
                }
            }

            return $"Import thành công: {dt.Rows.Count} dòng";
        }

        public static string HashSHA256(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        [AllowAnonymous]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login_OLD(string maNhanVien, string matKhau)
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

            return RedirectToAction("Index", "Dashboard");
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

            // ── BƯỚC 1: Thử đăng nhập qua API ──────────────────────────────────────
            string token = ApiLoginHelper.GetToken(maNhanVien, matKhau);
            if (!string.IsNullOrEmpty(token))
            {
                var empInfo = ApiLoginHelper.GetEmployeeByMaNV(token, maNhanVien);
                if (empInfo != null)
                {
                    // ── BƯỚC 2: Đồng bộ PhongBan ───────────────────────────────────
                    var phongBan = db.PhongBans
                        .FirstOrDefault(x => x.TenPhongBan == empInfo.phongban);

                    if (phongBan == null)
                    {
                        phongBan = new PhongBan
                        {
                            MaPhongBan = " ",
                            TenPhongBan = empInfo.phongban,
                            IsActive = true
                        };
                        db.PhongBans.Add(phongBan);
                        db.SaveChanges();
                    }

                    // ── BƯỚC 3: Đồng bộ Users (thêm mới hoặc cập nhật) ────────────
                    string passwordHash = SecurityHelper.Sha256Hash(matKhau);
                    var existingUser = db.Users
                        .FirstOrDefault(x => x.MaNhanVien == maNhanVien);

                    if (existingUser == null)
                    {
                        // Tạo mới
                        existingUser = new User
                        {
                            MaNhanVien = maNhanVien,
                            HoTen = empInfo.hoten,
                            Password = passwordHash,
                            RoleId = 3,
                            IsActive = true,
                            CreatedDate = DateTime.Now,
                            PhongBanId = phongBan.PhongBanId,
                            TinhTrangLamViec = int.TryParse(empInfo.tinhtranglamviec, out int tinhTrang) ? tinhTrang : 0
                        };
                        db.Users.Add(existingUser);
                    }
                    else
                    {
                        // Cập nhật
                        existingUser.HoTen = empInfo.hoten;
                        existingUser.PhongBanId = phongBan.PhongBanId;
                        existingUser.TinhTrangLamViec = int.TryParse(empInfo.tinhtranglamviec, out int tinhTrang) ? tinhTrang : 0;
                    }

                    db.SaveChanges();

                    // ── BƯỚC 4: Set cookie và redirect ─────────────────────────────
                    var loggedInUser = new LoggedInUser
                    {
                        UserId = existingUser.UserId,
                        MaNhanVien = existingUser.MaNhanVien,
                        UserName = existingUser.MaNhanVien,
                        HoTen = existingUser.HoTen,
                        PhongBanId = (int)existingUser.PhongBanId,
                        TenPhongBan = phongBan.TenPhongBan,
                        MaPhongBan = phongBan.MaPhongBan,
                        RoleId = existingUser.RoleId,
                    };

                    SetAuthCookie(loggedInUser);
                    return RedirectToAction("Index", "Dashboard");
                }
            }

            // ── BƯỚC 5: Fallback — đăng nhập local ─────────────────────────────────
            string localPasswordHash = SecurityHelper.Sha256Hash(matKhau);
            var user = db.Users
                .Where(x => x.MaNhanVien == maNhanVien
                         && x.Password == localPasswordHash
                         && (bool)x.IsActive)
                .Select(x => new LoggedInUser
                {
                    UserId = x.UserId,
                    MaNhanVien = x.MaNhanVien,
                    UserName = x.MaNhanVien,
                    HoTen = x.HoTen,
                    PhongBanId = (int)x.PhongBanId,
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

            SetAuthCookie(user);
            return RedirectToAction("Index", "Dashboard");
        }

        // ── Private helper tránh lặp code ──────────────────────────────────────────
        private void SetAuthCookie(LoggedInUser user)
        {
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