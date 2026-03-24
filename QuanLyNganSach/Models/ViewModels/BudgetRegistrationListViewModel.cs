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
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public string TenPhongBan { get; set; }
        public string NguoiDangKy { get; set; }
        //public int TrangThai { get; set; }
        public DateTime NgayTao { get; set; }
        // Thêm mới:
        public string SoToTrinhRaw { get; set; } // Giữ giá trị gốc để tính logic
        public int? WorkflowType { get; set; }
        public int TrangThaiPheDuyetGoc { get; set; } // TrangThaiPheDuyet của IsSupplementary=0
        public bool CoBoSungChuaDuyet { get; set; } // Có đợt bổ sung chưa duyệt
        public bool CoBoSungDaDuyet { get; set; } // Có ít nhất 1 đợt bổ sung đã duyệt
        public decimal? TongTienDo { get; set; }
        public int? DanhGiaChung { get; set; }

        // Property tính trạng thái theo thứ tự ưu tiên
        public int TrangThaiHienThi
        {
            get
            {
                // Chưa có chủ trương phê duyệt
                if (string.IsNullOrEmpty(SoToTrinhRaw))
                    return 0;

                // Đăng ký mới
                if (WorkflowType == null)
                    return 1;

                // Theo luồng chi phí sản xuất — ưu tiên tuyệt đối
                if (WorkflowType == 2)
                    return 5;

                // Chưa đủ hồ sơ — ưu tiên tuyệt đối
                if (WorkflowType == 3)
                    return 6;

                // Đang bổ sung ngân sách
                if (TrangThaiPheDuyetGoc == 2 && CoBoSungChuaDuyet)
                    return 4;

                // Đã phê duyệt ngân sách
                if (TrangThaiPheDuyetGoc == 2 && !CoBoSungChuaDuyet)
                    return 3;

                // Đang thực hiện xin ngân sách
                if (WorkflowType != null)
                    return 2;

                // Mặc định: Đăng ký mới
                return 1;
            }
        }
    }
}