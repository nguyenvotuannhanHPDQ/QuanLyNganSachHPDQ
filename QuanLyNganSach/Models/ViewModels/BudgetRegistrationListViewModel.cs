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

        // Property tính trạng thái theo thứ tự ưu tiên
        public int TrangThaiHienThi
        {
            get
            {
                // 1. Chưa có chủ trương phê duyệt
                if (string.IsNullOrEmpty(SoToTrinhRaw))
                    return 0;

                // 2. Đăng ký mới
                if (WorkflowType == null)
                    return 1;

                // 3. Đang bổ sung ngân sách
                if (TrangThaiPheDuyetGoc == 2 && CoBoSungChuaDuyet)
                    return 4;

                // 4. Đang thực hiện xin ngân sách
                if (TrangThaiPheDuyetGoc == 1)
                    return 2;

                // 5. Đã phê duyệt ngân sách
                if (TrangThaiPheDuyetGoc == 2 && !CoBoSungChuaDuyet)
                    return 3;

                // Mặc định: Đăng ký mới
                return 1;
            }
        }
    }
}