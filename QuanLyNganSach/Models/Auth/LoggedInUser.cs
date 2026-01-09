namespace QuanLyNganSach.Models.Auth
{
    public class LoggedInUser
    {
        public int UserId { get; set; }
        public string MaNhanVien { get; set; }
        public string UserName { get; set; }
        public string HoTen { get; set; }
        public int RoleId { get; set; }
        public int PhongBanId { get; set; }
        public string MaPhongBan { get; set; }
        public string TenPhongBan { get; set; }
    }
}