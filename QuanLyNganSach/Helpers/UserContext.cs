using QuanLyNganSach.Models.Auth;
using System.Web;

namespace QuanLyNganSach.Helpers
{
    public static class UserContext
    {
        public static LoggedInUser Current
            => HttpContext.Current?.Items["CurrentUser"] as LoggedInUser;
    }
}