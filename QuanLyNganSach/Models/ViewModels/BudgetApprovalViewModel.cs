using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    public class BudgetApprovalViewModel
    {
        public int BudgetApprovalId { get; set; }
        public int BudgetRegistrationId { get; set; }
        public int ProcessType { get; set; } = 1;

        public DateTime? NgayDuyetPDA { get; set; }
        public DateTime? NgayDuyetPKT { get; set; }
        public DateTime? NgayDuyetERPD { get; set; }
        public DateTime? NgayDuyetBTC { get; set; }
        public DateTime? NgayDuyetBGD { get; set; }

        public decimal? DuToanPheDuyet { get; set; }
        public decimal? DuToanGoc { get; set; } // Chỉ đọc từ BudgetRegistrations
        public string SoThongBao { get; set; }
        public string SoFMIO { get; set; }
        public int TrangThaiPheDuyet { get; set; } = 0;
        // *** THÊM MỚI ***
        public bool IsSupplementary { get; set; } = false;
        public int SupplementaryOrder { get; set; } = 0;
        public string LyDoBoSung { get; set; }
        public decimal? NganSachBoSung { get; set; } = 0;
    }
}