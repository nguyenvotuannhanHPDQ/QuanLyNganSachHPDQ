using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QuanLyNganSach.Models.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        public string MaNhanVien { get; set; }

        [Required]
        public string HoTen { get; set; }

        [Required]
        public int RoleId { get; set; }

        public IEnumerable<SelectListItem> Roles
        {
            get; set;
        }
    }
}