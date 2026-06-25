using System;
using System.Collections.Generic;
using System.Linq;

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
        public string SoToTrinhRaw { get; set; } // Giữ giá trị gốc để tính logic
        public int? WorkflowType { get; set; }
        public int TrangThaiPheDuyetGoc { get; set; } // TrangThaiPheDuyet của IsSupplementary=0
        public bool CoBoSungChuaDuyet { get; set; } // Có đợt bổ sung chưa duyệt
        public bool CoBoSungDaDuyet { get; set; } // Có ít nhất 1 đợt bổ sung đã duyệt
        public decimal? TongTienDo { get; set; }
        public int? DanhGiaChung { get; set; }
        public decimal TongTienDaDuyet { get; set; }
        public bool CoThongTinPheDuyet { get; set; }
        public int UserId { get; set; }
        public bool IsPhanNhiemUser { get; set; }

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

        // Navigation property thô để phục vụ tính toán trạng thái động
        public List<BudgetApprovalRawField> BudgetApprovals { get; set; } = new List<BudgetApprovalRawField>();

        // --- PROPERTY TỰ ĐỘNG TÁI CẤU TRÚC THEO LOGIC MỚI ---
        public string TrangThaiHienThiText
        {
            get
            {
                // 1. Tách biệt hồ sơ gốc và các đợt bổ sung
                var approvalGoc = BudgetApprovals.FirstOrDefault(x => !x.IsSupplementary);
                var danhSachBoSung = BudgetApprovals.Where(x => x.IsSupplementary).ToList();

                // 2. NHÓM 1: CHƯA XÁC NHẬN LUỒNG
                if (WorkflowType == null)
                {
                    return "Đăng ký mới"; // Case 1.1
                }

                // 3. NHÓM 2: HỒ SƠ BỊ TRẢ LẠI
                if (WorkflowType == 4)
                {
                    return "Chưa đủ hồ sơ"; // Case 2.1
                }

                // 4. NHÓM 3: LUỒNG CHI PHÍ SẢN XUẤT
                if (WorkflowType == 2)
                {
                    return "Theo luồng chi phí sản xuất"; // Case 3.1 -> 3.6
                }

                // 5. NHÓM 4: LUỒNG NGÂN SÁCH ĐẦU TƯ
                if (WorkflowType == 1)
                {
                    // TRƯỜNG HỢP: KHÔNG CÓ ĐỢT BỔ SUNG
                    if (!danhSachBoSung.Any())
                    {
                        if (approvalGoc?.NgayDuyetPDA == null && approvalGoc?.NgayDuyetBGD == null)
                            return "Chưa đủ hồ sơ"; // Case 4.1

                        if (approvalGoc?.NgayDuyetPDA != null && approvalGoc?.NgayDuyetBGD == null)
                            return "Đang thực hiện xin ngân sách"; // Case 4.2

                        if (approvalGoc?.NgayDuyetPDA != null && approvalGoc?.NgayDuyetBGD != null)
                            return "Đã phê duyệt ngân sách"; // Case 4.3
                    }
                    // TRƯỜNG HỢP: CÓ ĐỢT BỔ SUNG
                    else
                    {
                        // Quét trạng thái dựa trên sự tồn tại ngày của các đợt bổ sung
                        bool boSungChuaDuyetGi = danhSachBoSung.Any(a => a.NgayDuyetPDA == null && a.NgayDuyetBGD == null);
                        bool boSungDaDuyetPDA_ChuaBGD = danhSachBoSung.Any(a => a.NgayDuyetPDA != null && a.NgayDuyetBGD == null);
                        bool boSungDaDuyetCaHai = danhSachBoSung.Any(a => a.NgayDuyetPDA != null && a.NgayDuyetBGD != null);

                        if (boSungChuaDuyetGi)
                            return "Đã phê duyệt ngân sách"; // Case 4.4

                        if (boSungDaDuyetPDA_ChuaBGD)
                            return "Đang bổ sung ngân sách"; // Case 4.5

                        if (boSungDaDuyetCaHai)
                            return "Đã phê duyệt ngân sách"; // Case 4.6
                    }
                }

                return "Chưa xác định";
            }
        }

        // Khai báo class phụ trợ thu gọn để tối ưu bộ nhớ khi lấy dữ liệu từ DB
        public class BudgetApprovalRawField
        {
            public bool IsSupplementary { get; set; }
            public DateTime? NgayDuyetPDA { get; set; }
            public DateTime? NgayDuyetBGD { get; set; }
        }
    }
}