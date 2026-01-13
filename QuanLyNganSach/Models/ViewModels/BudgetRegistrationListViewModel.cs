using System;

namespace QuanLyNganSach.Models.ViewModels
{
    public class BudgetRegistrationListViewModel
    {
        public int BudgetRegistrationId { get; set; }
        public int PhongBanId { get; set; }
        public string MaHangMuc { get; set; }
        public string TenHangMuc { get; set; }
        public decimal DuToan { get; set; }
        public string SoToTrinh { get; set; }
        public string LyDoDauTu { get; set; }
        public string MoTaKyThuat { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public string TenPhongBan { get; set; }
        public string NguoiDangKy { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayTao { get; set; }
    }
}