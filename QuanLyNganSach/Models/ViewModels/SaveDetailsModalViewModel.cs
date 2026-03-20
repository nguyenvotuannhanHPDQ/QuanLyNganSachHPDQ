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
        // Thay thế ThongTinPheDuyet cũ
        public BudgetApprovalViewModel NganSachGoc { get; set; }
        public List<BudgetApprovalViewModel> DanhSachBoSung { get; set; }
            = new List<BudgetApprovalViewModel>();

        public ProgressConfigViewModel ThongTinTienDo { get; set; }
    }
}