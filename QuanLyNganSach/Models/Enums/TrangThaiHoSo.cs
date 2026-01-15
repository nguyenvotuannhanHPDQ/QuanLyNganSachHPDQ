using System.ComponentModel;

namespace QuanLyNganSach.Models.Enums
{
    /// <summary>
    /// Trạng thái đầy đủ thông tin của hồ sơ
    /// </summary>
    public enum TrangThaiHoSo
    {
        [Description("Còn thiếu thông tin")]
        ThieuThongTin = 0,

        [Description("Đủ thông tin")]
        DuThongTin = 1
    }
}