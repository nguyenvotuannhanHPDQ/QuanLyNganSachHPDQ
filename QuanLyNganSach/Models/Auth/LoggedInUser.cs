using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.Auth
{
    public class LoggedInUser
    {
        public string MaNhanVien { get; set; }
        public string UserName { get; set; }
        public int RoleId { get; set; }
    }
}