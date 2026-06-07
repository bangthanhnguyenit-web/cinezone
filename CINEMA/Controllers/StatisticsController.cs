using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using CINEMA.ViewModels;

namespace CINEMA.Controllers
{
    public class StatisticsController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public StatisticsController(CinemaContext context)
        {
            _context = context;
        }

        // ============================
        //  HIỂN THỊ TRANG THỐNG KÊ
        // ============================
        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var model = await BuildDashboard(from, to);
            return View(model);
        }

        // ============================
        //  HÀM XỬ LÝ THỐNG KÊ—TÁCH RIÊNG
        //  Dùng được cả Controller & ViewComponent
        // ============================
        public async Task<RevenueDashboardViewModel> BuildDashboard(DateTime? from, DateTime? to)
        {
            var model = new RevenueDashboardViewModel
            {
                FromDate = from,
                ToDate = to
            };

            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            // =====================================================
            //  🔹 Lấy đơn hàng đã thanh toán
            // =====================================================
            var paidOrders = _context.Orders
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .Where(o => o.Status != null &&
                            o.Status.ToLower().Contains("thanh toán") &&
                            o.CreatedAt != null);

            // 🔹 Lọc theo khoảng thời gian
            if (from.HasValue)
                paidOrders = paidOrders.Where(o => o.CreatedAt >= from.Value.Date);

            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                paidOrders = paidOrders.Where(o => o.CreatedAt <= toEnd);
            }

            // =====================================================
            //  🔹 Tổng doanh thu & số vé toàn kỳ
            // =====================================================
            model.TotalRevenue = await paidOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            model.TotalTickets = await paidOrders.SelectMany(o => o.Tickets).CountAsync();

            // =====================================================
            //  🔹 Hôm nay
            // =====================================================
            var todayOrders = paidOrders.Where(o => o.CreatedAt!.Value.Date == today);
            model.TodayRevenue = await todayOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            model.TodayTickets = await todayOrders.SelectMany(o => o.Tickets).CountAsync();

            // =====================================================
            //  🔹 Tháng này
            // =====================================================
            var monthOrders = paidOrders.Where(o => o.CreatedAt!.Value >= startOfMonth);
            model.MonthRevenue = await monthOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            model.MonthTickets = await monthOrders.SelectMany(o => o.Tickets).CountAsync();

            // =====================================================
            // 🔹 Combo bắp nước tổng
            // =====================================================
            var comboData = await _context.OrderCombos
                .Include(oc => oc.Order)
                .Where(oc => oc.Order != null &&
                             oc.Order.Status != null &&
                             oc.Order.Status.ToLower().Contains("thanh toán"))
                .ToListAsync();

            model.ComboRevenue = comboData.Sum(oc =>
                (oc.UnitPrice ?? 0) * (oc.Quantity ?? 0));

            model.ComboSold = comboData.Sum(oc =>
                oc.Quantity ?? 0);

            // =====================================================
            // 🔥 THỐNG KÊ CHI TIẾT COMBO
            // =====================================================

            var comboStats = await paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Combo != null)
                .GroupBy(oc => new
                {
                    oc.Combo.ComboId,
                    oc.Combo.Name
                })
                .Select(g => new
                {
                    ComboName = g.Key.Name,

                    QuantitySold = g.Sum(x =>
                        x.Quantity ?? 0),

                    Revenue = g.Sum(x =>
                        (x.UnitPrice ?? 0) *
                        (x.Quantity ?? 0))
                })
                .OrderByDescending(x => x.QuantitySold)
                .ToListAsync();

            var totalComboSold = comboStats.Sum(x => x.QuantitySold);

            // Bảng thống kê
            foreach (var item in comboStats)
            {
                model.ComboStatistics.Add(
                    new ComboStatisticViewModel
                    {
                        ComboName = item.ComboName ?? "",

                        QuantitySold = item.QuantitySold,

                        Revenue = item.Revenue,

                        Percentage = totalComboSold == 0
                            ? 0
                            : Math.Round(
                                item.QuantitySold * 100.0 /
                                totalComboSold,
                                2)
                    });
            }

            // Combo bán chạy nhất
            var bestCombo = comboStats.FirstOrDefault();

            if (bestCombo != null)
            {
                model.BestSellingCombo =
                    bestCombo.ComboName;

                model.BestSellingQuantity =
                    bestCombo.QuantitySold;
            }

            // Pie chart
            foreach (var item in comboStats)
            {
                model.ComboPieLabels.Add(
                    item.ComboName ?? "");

                model.ComboPieValues.Add(
                    item.QuantitySold);
            }

            // =====================================================
            // 🔹 Doanh thu & vé theo từng ngày
            // =====================================================
            var dailyStats = await paidOrders
                .Select(o => new
                {
                    Date = o.CreatedAt!.Value.Date,
                    Revenue = o.TotalAmount ?? 0,
                    Tickets = o.Tickets.Count
                })
                .GroupBy(x => x.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    Tickets = g.Sum(x => x.Tickets)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            foreach (var d in dailyStats)
            {
                model.LabelsByDate.Add(d.Date.ToString("dd/MM"));
                model.RevenueByDate.Add(d.Revenue);
                model.TicketCountByDate.Add(d.Tickets);
            }

            // =====================================================
            // 🔹 Doanh thu theo tháng
            // =====================================================
            var monthlyStats = await paidOrders
                .Select(o => new
                {
                    Year = o.CreatedAt!.Value.Year,
                    Month = o.CreatedAt.Value.Month,
                    Revenue = o.TotalAmount ?? 0,
                    Tickets = o.Tickets.Count
                })
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => x.Revenue),
                    Tickets = g.Sum(x => x.Tickets)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            foreach (var m in monthlyStats)
            {
                model.LabelsByMonth.Add($"{m.Month}/{m.Year}");
                model.RevenueByMonth.Add(m.Revenue);
                model.TicketCountByMonth.Add(m.Tickets);
            }

            // =====================================================
            // 🔹 Doanh thu theo năm
            // =====================================================
            var yearlyStats = await paidOrders
                .Select(o => new
                {
                    Year = o.CreatedAt!.Value.Year,
                    Revenue = o.TotalAmount ?? 0,
                    Tickets = o.Tickets.Count
                })
                .GroupBy(x => x.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    Tickets = g.Sum(x => x.Tickets)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToListAsync();

            yearlyStats.Reverse();

            foreach (var y in yearlyStats)
            {
                model.LabelsByYear.Add(y.Year.ToString());
                model.RevenueByYear.Add(y.Revenue);
                model.TicketCountByYear.Add(y.Tickets);
            }

            // =====================================================
            // 🔹 Combo theo tháng
            // =====================================================
            var comboByMonth = await paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Order != null && oc.Order.CreatedAt != null)
                .GroupBy(oc => new
                {
                    oc.Order.CreatedAt!.Value.Year,
                    oc.Order.CreatedAt.Value.Month
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => (x.UnitPrice ?? 0) * (x.Quantity ?? 0)),
                    Quantity = g.Sum(x => x.Quantity ?? 0)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            foreach (var item in comboByMonth)
            {
                model.ComboLabelsByMonth.Add($"{item.Month}/{item.Year}");
                model.ComboRevenueByMonth.Add(item.Revenue);
                model.ComboQuantityByMonth.Add(item.Quantity);
            }

            // =====================================================
            // 🔹 Top 5 phim doanh thu cao
            // =====================================================
            var revenueByMovie = await paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime != null && t.Showtime.Movie != null)
                .GroupBy(t => t.Showtime.Movie.Title)
                .Select(g => new
                {
                    MovieTitle = g.Key,
                    Revenue = g.Sum(x => x.Price) ?? 0
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            foreach (var mv in revenueByMovie)
            {
                model.MovieLabels.Add(mv.MovieTitle);
                model.RevenueByMovie.Add(mv.Revenue);
            }
            // =====================================================
            // 🔥 THỐNG KÊ SỐ LƯỢNG ĐƠN
            // =====================================================

            var allOrders = _context.Orders.AsQueryable();

            // lọc theo ngày giống paidOrders
            if (from.HasValue)
                allOrders = allOrders.Where(o => o.CreatedAt >= from.Value.Date);

            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                allOrders = allOrders.Where(o => o.CreatedAt <= toEnd);
            }

            // 🔥 COUNT
            // 🔥 COUNT
            model.PaidOrders = await allOrders.CountAsync(o =>
                o.Status != null && o.Status.ToLower().Contains("thanh toán"));

            model.PendingOrders = await allOrders.CountAsync(o =>
                o.Status != null && o.Status.ToLower().Contains("chờ"));

            model.CancelledOrders = await allOrders.CountAsync(o =>
                o.Status != null &&
                (o.Status.ToLower().Contains("hủy") ||
                 o.Status.ToLower().Contains("thất bại")));
            return model;
        }
       
        public async Task<IActionResult> AdvancedStats(int? tId, int? mId, int? tId2, DateTime? from, DateTime? to)
        {
            var model = await BuildDashboard(from, to);


            // --- LỌC BIỂU ĐỒ 1 (Rạp) ---
            var query1 = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (tId.HasValue)
            {
                query1 = query1.Where(t => t.Showtime.Auditorium.TheaterId == tId.Value);
            }

            var theaterStats = await query1.GroupBy(t => t.Showtime.Auditorium.Theater.Name)
                                           .Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync();
            model.CinemaLabels = theaterStats.Select(x => x.Name ?? "N/A").ToList();
            model.CinemaRevenue = theaterStats.Select(x => (decimal)x.Count).ToList();

            // --- LỌC BIỂU ĐỒ 2 (Suất chiếu) ---
            var query2 = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (mId.HasValue) query2 = query2.Where(t => t.Showtime.MovieId == mId.Value);
            if (tId2.HasValue) query2 = query2.Where(t => t.Showtime.Auditorium.TheaterId == tId2.Value);

            var showtimeStats = await query2.OrderByDescending(t => t.Showtime.StartTime).Take(10)
                                            .GroupBy(t => new { t.Showtime.Movie.Title, t.Showtime.StartTime })
                                            .Select(g => new { Info = $"{g.Key.Title} ({g.Key.StartTime:HH:mm})", Count = g.Count() }).ToListAsync();

            model.ShowtimeLabels = showtimeStats.Select(x => x.Info).ToList();
            model.TicketsByShowtime = showtimeStats.Select(x => x.Count).ToList();

            ViewBag.Theaters = await _context.Theaters.ToListAsync();
            ViewBag.Movies = await _context.Movies.ToListAsync();

            // Thêm đoạn này vào cuối hàm AdvancedStats (trước khi return View)
            var totalSeats = await _context.Seats.CountAsync();
            // Tính dựa trên query tổng (query2) hoặc query toàn bộ
            var soldSeats = await _context.Tickets.CountAsync(t => t.Order.Status.Contains("thanh toán"));
            model.GlobalOccupancyRate = totalSeats > 0 ? Math.Round((double)soldSeats / totalSeats * 100, 2) : 0;

            return View(model);
        }
        [HttpGet]
        public async Task<JsonResult> GetTheaterData(int? tId)
        {
            var query = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (tId.HasValue && tId > 0) query = query.Where(t => t.Showtime.Auditorium.TheaterId == tId.Value);

            var data = await query.GroupBy(t => t.Showtime.Auditorium.Theater.Name)
                                  .Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync();
            return Json(new { labels = data.Select(x => x.Name), values = data.Select(x => x.Count) });
        }

        [HttpGet]
        public async Task<JsonResult> GetShowtimeData(int? mId, int? tId2)
        {
            var query = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (mId.HasValue && mId > 0) query = query.Where(t => t.Showtime.MovieId == mId.Value);
            if (tId2.HasValue && tId2 > 0) query = query.Where(t => t.Showtime.Auditorium.TheaterId == tId2.Value);

            var data = await query.OrderByDescending(t => t.Showtime.StartTime)
                                  .Take(10)
                                  .GroupBy(t => new { t.Showtime.Movie.Title, t.Showtime.StartTime })
                                  .Select(g => new {
                                      Info = $"{g.Key.Title} ({g.Key.StartTime:HH:mm})",
                                      Count = g.Count()
                                  })
                                  .ToListAsync();

            // Đảm bảo dữ liệu không bao giờ là null
            return Json(new
            {
                labels = data.Select(x => x.Info).ToList(),
                values = data.Select(x => x.Count).ToList()
            });
        }


    }
}
