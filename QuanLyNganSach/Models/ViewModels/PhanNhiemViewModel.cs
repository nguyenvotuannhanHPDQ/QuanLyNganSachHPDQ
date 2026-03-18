using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    public class PhanNhiemViewModel
    {
        public int? PhongBanId { get; set; }

        // Chọn từ dropdown — nullable vì có thể nhập tay
        public int? ChucNangNhiemVuId { get; set; }

        // Nhập tay — nullable vì có thể chọn từ dropdown
        public string TenChucNangNhapTay { get; set; }

        public string Email { get; set; }
        public string GhiChu { get; set; }
        public string TenPhongBan { get; set; }   // Thêm mới
        public string TenChucNang { get; set; }   // Thêm mới
    }
}