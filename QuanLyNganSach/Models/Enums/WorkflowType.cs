using System.ComponentModel;

namespace QuanLyNganSach.Models.Enums
{
    /// <summary>
    /// Loại luồng quy trình
    /// </summary>
    public enum WorkflowType
    {
        [Description("Chưa xác định")]
        ChuaXacDinh = 0,

        [Description("Theo luồng ngân sách đầu tư")]
        NganSachDauTu = 1,

        [Description("Theo luồng chi phí sản xuất")]
        ChiPhiSanXuat = 2
    }
}