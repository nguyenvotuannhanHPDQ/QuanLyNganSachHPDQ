using QuanLyNganSach.Filters;
using System.Web.Mvc;

namespace QuanLyNganSach
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new CurrentUserFilter());
        }
    }
}
