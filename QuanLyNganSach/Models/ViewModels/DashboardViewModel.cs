using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuanLyNganSach.Models.ViewModels
{
    /// <summary>
    /// ViewModel cho Dashboard Tổng quan
    /// </summary>
    public class DashboardViewModel
    {
        public DashboardViewModel()
        {
            TopPerformers = new List<TopPerformerViewModel>();
            UserTaskCounts = new List<UserTaskCountViewModel>();
            TasksByStatus = new List<TaskStatusCountViewModel>();
            CompletionTrend = new List<MonthlyCompletionViewModel>();
            UpcomingDeadlines = new List<UpcomingTaskViewModel>();
            OverdueTasks = new List<OverdueTaskGroupViewModel>();
        }

        // Filter
        public string SelectedPeriod { get; set; } // "current", "previous", "3months"

        // Cards - 4 metrics chính
        public int TotalInProgressTasks { get; set; }
        public int CompletedTasksThisMonth { get; set; }
        //public int OverdueTasks { get; set; }
        public decimal AverageKPIScore { get; set; }

        // Charts data
        public List<UserTaskCountViewModel> UserTaskCounts { get; set; } // Bar chart - Top 10
        public List<TaskStatusCountViewModel> TasksByStatus { get; set; } // Pie chart
        public List<MonthlyCompletionViewModel> CompletionTrend { get; set; } // Line chart - 3 months

        // Tables
        public List<TopPerformerViewModel> TopPerformers { get; set; } // Top 5 KPI
        public List<UpcomingTaskViewModel> UpcomingDeadlines { get; set; } // Sắp đến hạn
        public List<OverdueTaskGroupViewModel> OverdueTasks { get; set; } // Quá hạn nhóm theo User
    }

    /// <summary>
    /// Top Performer (User có KPI cao nhất)
    /// </summary>
    public class TopPerformerViewModel
    {
        public int Rank { get; set; }
        public string UserName { get; set; }
        public decimal AverageKPI { get; set; }
    }

    /// <summary>
    /// Số lượng Task theo User (cho Bar chart)
    /// </summary>
    public class UserTaskCountViewModel
    {
        public string UserName { get; set; }
        public int TaskCount { get; set; }
    }

    /// <summary>
    /// Phân bố Task theo Status (cho Pie chart)
    /// </summary>
    public class TaskStatusCountViewModel
    {
        public string Status { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Xu hướng hoàn thành theo tháng (cho Line chart)
    /// </summary>
    public class MonthlyCompletionViewModel
    {
        public string MonthYear { get; set; } // "01/2026"
        public int CompletedCount { get; set; }
    }

    /// <summary>
    /// Task sắp đến hạn (trong 3 ngày)
    /// </summary>
    public class UpcomingTaskViewModel
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public string UserName { get; set; }
        public DateTime Deadline { get; set; }
        public int DaysRemaining { get; set; }
        public string DeadlineFormatted => Deadline.ToString("dd/MM/yyyy");
        public string DaysRemainingText
        {
            get
            {
                if (DaysRemaining == 0) return "Hôm nay";
                if (DaysRemaining == 1) return "1 ngày";
                return $"{DaysRemaining} ngày";
            }
        }
    }

    /// <summary>
    /// Task quá hạn nhóm theo User (cho Accordion)
    /// </summary>
    public class OverdueTaskGroupViewModel
    {
        public OverdueTaskGroupViewModel()
        {
            Tasks = new List<OverdueTaskItemViewModel>();
        }

        public int UserId { get; set; }
        public string UserName { get; set; }
        public int OverdueCount { get; set; }
        public List<OverdueTaskItemViewModel> Tasks { get; set; }
    }

    /// <summary>
    /// Chi tiết Task quá hạn
    /// </summary>
    public class OverdueTaskItemViewModel
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public DateTime ExpectedEndDate { get; set; }
        public int DaysOverdue { get; set; }
        public string ExpectedEndDateFormatted => ExpectedEndDate.ToString("dd/MM/yyyy");
        public string DaysOverdueText => $"{DaysOverdue} ngày";
    }
}