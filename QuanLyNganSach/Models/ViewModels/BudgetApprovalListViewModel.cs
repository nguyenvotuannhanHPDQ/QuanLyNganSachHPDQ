using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    /// <summary>
    /// ViewModel cho danh sách phê duyệt
    /// </summary>
    public class BudgetApprovalListViewModel
    {
        public int BudgetApprovalId { get; set; }
        public int ApprovalProcessType { get; set; }
        public string ApprovalProcessTypeName { get; set; }
        public decimal ApprovedAmount { get; set; }
        public string NotificationNumber { get; set; }
        public string FmIoNumber { get; set; }

        public DateTime? PhongDuAnDate { get; set; }
        public DateTime? PhongKeToanDate { get; set; }
        public DateTime? ERPDDate { get; set; }
        public DateTime? BanTaiChinhDate { get; set; }
        public DateTime? BanGiamDocDate { get; set; }

        public DateTime CreatedDate { get; set; }
        public string CreatedByName { get; set; }

        // Helper properties
        public bool IsCompleted => BanGiamDocDate.HasValue;
        public int CompletedSteps => GetCompletedStepsCount();
        public int TotalSteps => ApprovalProcessType == 1 ? 3 : 5;

        private int GetCompletedStepsCount()
        {
            int count = 0;
            if (PhongDuAnDate.HasValue) count++;
            if (PhongKeToanDate.HasValue) count++;
            if (ApprovalProcessType == 2)
            {
                if (ERPDDate.HasValue) count++;
                if (BanTaiChinhDate.HasValue) count++;
            }
            if (BanGiamDocDate.HasValue) count++;
            return count;
        }
    }

    /// <summary>
    /// ViewModel cho form tạo/sửa phê duyệt
    /// </summary>
    public class BudgetApprovalFormViewModel
    {
        public int? BudgetApprovalId { get; set; }
        public int BudgetRegistrationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại quy trình")]
        public int ApprovalProcessType { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tiền phê duyệt")]
        [Range(1, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        public decimal ApprovedAmount { get; set; }

        public decimal OriginalBudget { get; set; } // For comparison

        public string NotificationNumber { get; set; }
        public string FmIoNumber { get; set; }

        public DateTime? PhongDuAnDate { get; set; }
        public DateTime? PhongKeToanDate { get; set; }
        public DateTime? ERPDDate { get; set; }
        public DateTime? BanTaiChinhDate { get; set; }
        public DateTime? BanGiamDocDate { get; set; }
    }

    /// <summary>
    /// ViewModel cho response từ GetBudgetApprovals
    /// </summary>
    public class BudgetApprovalsDataViewModel
    {
        public List<BudgetApprovalListViewModel> Approvals { get; set; }
        public decimal OriginalBudget { get; set; }
        public bool CanEdit { get; set; }

        public BudgetApprovalsDataViewModel()
        {
            Approvals = new List<BudgetApprovalListViewModel>();
        }
    }
}