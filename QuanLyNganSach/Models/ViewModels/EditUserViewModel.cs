using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace QuanLyNganSach.Models.ViewModels
{
    public class EditUserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã nhân viên")]
        public string MaNhanVien { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn quyền hệ thống")]
        public int RoleId { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> Roles { get; set; }
    }
}