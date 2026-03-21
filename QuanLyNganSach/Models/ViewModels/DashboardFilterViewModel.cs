using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    public class DashboardFilterViewModel
    {
        public int? ProjectAreaId { get; set; }
        public int? PhongBanId { get; set; }
        public int? Nam { get; set; }
        public int? PriorityLevelId { get; set; }
        public int? CategoryTypeId { get; set; }
    }

    public class DashboardSummaryViewModel
    {
        // Card tổng quan
        public decimal TongNganSach { get; set; }
        public int SoHangMuc { get; set; }
        public int HangMucChuaDuyet { get; set; }

        // Biểu đồ tròn
        public decimal TongChuaTrinh { get; set; }
        public decimal TongDangTrinh { get; set; }
        public decimal TongDaPheduyet { get; set; }
    }
}