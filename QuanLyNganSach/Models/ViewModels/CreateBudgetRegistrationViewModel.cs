using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace QuanLyNganSach.Models.ViewModels
{
    public class CreateBudgetRegistrationViewModel
    {
        [Required]
        public string MaHangMuc { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên hạng mục")]
        public string TenHangMuc { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập dự toán")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Vui lòng nhập dự toán hợp lệ")]
        public decimal DuToan { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tờ trình")]
        public string SoToTrinh { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại hạng mục")]
        public int CategoryTypeId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn mức ưu tiên")]
        public int PriorityLevelId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng nhập số lượng hợp lệ")]
        public int SoLuong { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do đầu tư")]
        public string LyDoDauTu { get; set; }

        public string MoTaKyThuat { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime NgayBatDau { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ngày kết thúc")]
        [DataType(DataType.Date)]
        public DateTime NgayKetThuc { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hồ sơ căn cứ")]
        public HttpPostedFileBase HoSoCanCu { get; set; }

        public string LinkTaiLieuLienQuan { get; set; }

        /* Dropdowns */
        public IEnumerable<SelectListItem> CategoryTypes { get; set; }
        public IEnumerable<SelectListItem> PriorityLevels { get; set; }
    }

}