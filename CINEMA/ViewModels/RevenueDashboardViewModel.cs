using System;
using System.Collections.Generic;

namespace CINEMA.ViewModels
{
    public class RevenueDashboardViewModel
    {
        // ==========================================
        // 1. TỔNG QUAN (Git + Local)
        // ==========================================
        public decimal TotalRevenue { get; set; }
        public int TotalTickets { get; set; }

        public decimal TodayRevenue { get; set; }
        public int TodayTickets { get; set; }

        public decimal MonthRevenue { get; set; }
        public int MonthTickets { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // --- KPI Trung bình đơn (AOV) từ Local ---
        public decimal AvgOrderValue { get; set; }
        public decimal AvgOrderValueToday { get; set; }
        public decimal AvgOrderValueMonth { get; set; }
        public decimal AvgOrderValueQuarter { get; set; }
        public decimal AvgOrderValueYear { get; set; }
        public decimal RevenueCurrentMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }

        // --- Trạng thái đơn hàng ---
        public int PaidOrders { get; set; }
        public int PendingOrders { get; set; }
        public int CancelledOrders { get; set; }

        // ==========================================
        // 2. BIỂU ĐỒ THEO THỜI GIAN
        // ==========================================
        // --- Theo ngày ---
        public List<string> LabelsByDate { get; set; } = new();
        public List<decimal> RevenueByDate { get; set; } = new();
        public List<int> TicketCountByDate { get; set; } = new();
        public List<int> OrderCountByDate { get; set; } = new(); // Local: Đếm số đơn

        // --- Theo tháng ---
        public List<string> LabelsByMonth { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();
        public List<int> TicketCountByMonth { get; set; } = new();

        // --- Theo năm ---
        public List<string> LabelsByYear { get; set; } = new();
        public List<decimal> RevenueByYear { get; set; } = new();
        public List<int> TicketCountByYear { get; set; } = new();

        // ==========================================
        // 3. THỐNG KÊ PHIM & DANH HIỆU (BẢNG VÀNG)
        // ==========================================
        public List<string> MovieLabels { get; set; } = new();
        public List<decimal> RevenueByMovie { get; set; } = new();
        public List<string> MovieTicketLabels { get; set; } = new();
        public List<int> TicketsByMovie { get; set; } = new();

        // --- Danh hiệu từ Local ---
        public string TopSellingMovie { get; set; } = string.Empty;
        public int TopSellingTickets { get; set; }
        public string LeastSellingMovie { get; set; } = string.Empty;
        public int LeastSellingTickets { get; set; }
        public string TopRevenueMovie { get; set; } = string.Empty;
        public decimal TopRevenueAmount { get; set; }

        // --- Trạng thái phim & Thể loại (Local) ---
        public int NowShowingCount { get; set; }
        public int ComingSoonCount { get; set; }
        public List<GenreStatisticViewModel> GenreStatistics { get; set; } = new();

        // ==========================================
        // 4. THỐNG KÊ COMBO CHI TIẾT
        // ==========================================
        public decimal ComboRevenue { get; set; }
        public int ComboSold { get; set; }
        public string? BestSellingCombo { get; set; }
        public int BestSellingQuantity { get; set; }

        public List<ComboStatisticViewModel> ComboStatistics { get; set; } = new();

        public List<string> ComboPieLabels { get; set; } = new();
        public List<int> ComboPieValues { get; set; } = new();

        public List<string> ComboLabelsByMonth { get; set; } = new();
        public List<decimal> ComboRevenueByMonth { get; set; } = new();
        public List<int> ComboQuantityByMonth { get; set; } = new();

        // Thêm vào RevenueDashboardViewModel.cs
        public List<string> CinemaLabels { get; set; } = new();
        public List<decimal> CinemaRevenue { get; set; } = new();

        public List<ShowtimeStatisticViewModel> ShowtimeStatistics { get; set; } = new();
        public List<string> ShowtimeLabels { get; set; } = new();
        public List<int> TicketsByShowtime { get; set; } = new();
        public List<decimal> RevenueByShowtime { get; set; } = new();

        public double GlobalOccupancyRate { get; set; }
        public int? SelectedTheaterId { get; set; }
        public List<CINEMA.Models.Ticket> Tickets { get; set; } = new();



        public List<string> MovieTicketLabels { get; set; } = new();
        public List<int> TicketsByMovie { get; set; } = new();

        // =========================
        // THỐNG KÊ COMBO CHI TIẾT
        // =========================

        public List<ComboStatisticViewModel> ComboStatistics { get; set; }
            = new();

        public string? BestSellingCombo { get; set; }

        public int BestSellingQuantity { get; set; }

        public List<string> ComboPieLabels { get; set; }
            = new();

        public List<int> ComboPieValues { get; set; }
            = new();
        // =========================
        // DOANH THU THEO SUẤT CHIẾU
        // =========================

    // =====================================================
    // 🔹 CLASS PHỤ THỂ LOẠI (Đã dọn dẹp lỗi lồng danh sách)
    // =====================================================
    public class GenreStatisticViewModel
    {
        public string GenreName { get; set; } = string.Empty;
        public int MovieCount { get; set; }
        public double Percentage { get; set; }
    }
}