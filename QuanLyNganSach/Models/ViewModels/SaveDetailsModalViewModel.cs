using QuanLyNganSach.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    public class SaveDetailsModalViewModel
    {
        public int BudgetRegistrationId { get; set; }
        public int? WorkflowType { get; set; }
        public List<PhanNhiemViewModel> DanhSachPhanNhiem { get; set; }
            = new List<PhanNhiemViewModel>();
        public BudgetApprovalViewModel NganSachGoc { get; set; }
        public List<BudgetApprovalViewModel> DanhSachBoSung { get; set; }
            = new List<BudgetApprovalViewModel>();

        public ProgressConfigViewModel ThongTinTienDo { get; set; }
        public bool IsPhanNhiemSearchMode { get; set; }
        // --- THÊM MỚI CÁC TRƯỜNG CỦA PHIẾU ĐĂNG KÝ ---
        public int ProjectAreaId { get; set; }
        public string TenHangMuc { get; set; }
        public decimal DuToan { get; set; }
        public string SoToTrinh { get; set; }
        public int CategoryTypeId { get; set; }
        public int PriorityLevelId { get; set; }
        public int SoLuong { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public string LyDoDauTu { get; set; }
        public string MoTaKyThuat { get; set; }
        public string LinkTaiLieuLienQuan { get; set; }
        public SaveProgressLogDto NhatKyTienDo { get; set; }
    }
}