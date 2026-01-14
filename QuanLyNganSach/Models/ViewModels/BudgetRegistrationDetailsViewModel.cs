using System;
using System.Collections.Generic;

namespace QuanLyNganSach.Models.ViewModels
{
    public class BudgetRegistrationDetailsViewModel
    {
        public int BudgetRegistrationId { get; set; }
        public string TenNguoiDangKy { get; set; }
        public string TenPhongBan { get; set; }
        public string MaHangMuc { get; set; }
        public string TenHangMuc { get; set; }
        public decimal DuToan { get; set; }
        public string SoToTrinh { get; set; }
        public int SoLuong { get; set; }
        public string LyDoDauTu { get; set; }
        public string MoTaKyThuat { get; set; }
        public string LinkTaiLieuLienQuan { get; set; }
        public int CategoryTypeId { get; set; }
        public string CategoryTypeName { get; set; }
        public int PriorityLevelId { get; set; }
        public string PriorityLevelName { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public DateTime NgayTao { get; set; }

        // Attachments
        public List<BudgetAttachmentViewModel> Attachments { get; set; }

        // Constructor
        public BudgetRegistrationDetailsViewModel()
        {
            Attachments = new List<BudgetAttachmentViewModel>();
        }
    }

    /// <summary>
    /// ViewModel cho file đính kèm
    /// </summary>
    public class BudgetAttachmentViewModel
    {
        public int AttachmentId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileExtension { get; set; }
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedDate { get; set; }
    }
}