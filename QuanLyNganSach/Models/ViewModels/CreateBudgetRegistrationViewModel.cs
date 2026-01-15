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

        [Range(0.01, double.MaxValue, ErrorMessage = "Vui lòng nhập dự toán hợp lệ")]
        public decimal DuToan { get; set; }

        public string SoToTrinh { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại hạng mục")]
        public int CategoryTypeId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn mức ưu tiên")]
        public int PriorityLevelId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng nhập số lượng hợp lệ")]
        public int SoLuong { get; set; }

        public string LyDoDauTu { get; set; }

        public string MoTaKyThuat { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NgayBatDau { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NgayKetThuc { get; set; }


        public HttpPostedFileBase HoSoCanCu { get; set; }

        public string LinkTaiLieuLienQuan { get; set; }

        /* Dropdowns */
        public IEnumerable<SelectListItem> CategoryTypes { get; set; }
        public IEnumerable<SelectListItem> PriorityLevels { get; set; }
    }

}