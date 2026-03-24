using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    public class ProgressConfigViewModel
    {
        public int ProgressConfigId { get; set; }
        public int BudgetRegistrationId { get; set; }
        public decimal TiTrongXayDung { get; set; }
        public decimal TiTrongKetCauThep { get; set; }
        public decimal TiTrongLapDatThietBi { get; set; }
        public decimal TiTrongHangMucKhac { get; set; }
        public int? DanhGiaChung { get; set; }
        public List<ProgressAreaViewModel> DanhSachKhuVuc { get; set; }
            = new List<ProgressAreaViewModel>();
        public decimal? TongTienDo { get; set; }
    }

    public class ProgressAreaViewModel
    {
        public int ProgressAreaId { get; set; }
        public string TenKhuVuc { get; set; }
        public int SortOrder { get; set; }
        public List<ProgressAreaItemViewModel> DanhSachDong { get; set; }
            = new List<ProgressAreaItemViewModel>();
    }

    public class ProgressAreaItemViewModel
    {
        public int ProgressAreaItemId { get; set; }
        public string HangMucCongViec { get; set; } // Chọn từ dropdown
        public string HangMucNhapTay { get; set; } // Nhập tay
        public string DVT { get; set; }
        public decimal KLHD { get; set; }
        public decimal KLTT { get; set; }
        public string GhiChu { get; set; }
        public int SortOrder { get; set; }
    }
}