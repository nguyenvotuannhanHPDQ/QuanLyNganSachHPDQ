using System;

namespace QuanLyNganSach.Models.ViewModels
{
    public class UserListViewModel
    {
        public int UserId { get; set; }
        public string MaNhanVien { get; set; }
        public string HoTen { get; set; }
        public string TenPhongBan { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}