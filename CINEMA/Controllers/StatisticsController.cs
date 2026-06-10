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
        //  HÀM XỬ LÝ THỐNG KÊ GỘP (GIT + LOCAL)
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
            var lastMonthStart = startOfMonth.AddMonths(-1);

            // =====================================================
            // 1. LẤY ĐƠN HÀNG ĐÃ THANH TOÁN (Kéo về bộ nhớ để tính toán nhiều tiêu chí)
            // =====================================================
            var paidOrdersQuery = _context.Orders
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium) // Từ Git
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .Where(o => o.Status != null && o.Status.ToLower().Contains("thanh toán") && o.CreatedAt != null);

            if (from.HasValue)
                paidOrdersQuery = paidOrdersQuery.Where(o => o.CreatedAt >= from.Value.Date);

            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                paidOrdersQuery = paidOrdersQuery.Where(o => o.CreatedAt <= toEnd);
            }

            var paidOrders = await paidOrdersQuery.ToListAsync();

            // =====================================================
            // 2. TỔNG DOANH THU & KPI (Từ Local & Git)
            // =====================================================
            model.TotalRevenue = paidOrders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.TotalTickets = paidOrders.SelectMany(o => o.Tickets).Count();

            var totalOrdersCount = paidOrders.Count;
            model.AvgOrderValue = totalOrdersCount > 0 ? (model.TotalRevenue / totalOrdersCount) : 0;

            model.RevenueCurrentMonth = paidOrders.Where(o => o.CreatedAt >= startOfMonth).Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.RevenueLastMonth = paidOrders.Where(o => o.CreatedAt >= lastMonthStart && o.CreatedAt < startOfMonth).Sum(o => (decimal?)o.TotalAmount) ?? 0;

            // =====================================================
            // 3. THỐNG KÊ HÔM NAY, THÁNG NÀY & TRUNG BÌNH ĐƠN
            // =====================================================
            var todayOrders = paidOrders.Where(o => o.CreatedAt!.Value.Date == today).ToList();
            model.TodayRevenue = todayOrders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.TodayTickets = todayOrders.SelectMany(o => o.Tickets).Count(); // Từ Git

            var monthOrders = paidOrders.Where(o => o.CreatedAt!.Value >= startOfMonth).ToList();
            model.MonthRevenue = monthOrders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.MonthTickets = monthOrders.SelectMany(o => o.Tickets).Count(); // Từ Git

            // KPI Trung bình đơn (Từ Local)
            model.AvgOrderValueToday = todayOrders.Any() ? todayOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;
            model.AvgOrderValueMonth = monthOrders.Any() ? monthOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;

            int currentQuarter = (today.Month - 1) / 3 + 1;
            var quarterOrders = paidOrders.Where(o => o.CreatedAt?.Year == today.Year && ((o.CreatedAt!.Value.Month - 1) / 3 + 1) == currentQuarter).ToList();
            model.AvgOrderValueQuarter = quarterOrders.Any() ? quarterOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;

            var yearOrders = paidOrders.Where(o => o.CreatedAt?.Year == today.Year).ToList();
            model.AvgOrderValueYear = yearOrders.Any() ? yearOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;

            // =====================================================
            // 4. SỐ LƯỢNG ĐƠN HÀNG TOÀN BỘ TRẠNG THÁI (Gộp Git & Local)
            // =====================================================
            var allOrdersQuery = _context.Orders.AsQueryable();
            if (from.HasValue) allOrdersQuery = allOrdersQuery.Where(o => o.CreatedAt >= from.Value.Date);
            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                allOrdersQuery = allOrdersQuery.Where(o => o.CreatedAt <= toEnd);
            }

            model.PaidOrders = await allOrdersQuery.CountAsync(o => o.Status != null && o.Status.ToLower().Contains("thanh toán"));
            model.PendingOrders = await allOrdersQuery.CountAsync(o => o.Status != null && o.Status.ToLower().Contains("chờ"));
            model.CancelledOrders = await allOrdersQuery.CountAsync(o => o.Status != null && (o.Status.ToLower().Contains("hủy") || o.Status.ToLower().Contains("thất bại"))); // Từ Git

            // =====================================================
            // 5. THỐNG KÊ PHIM & DOANH THU PHIM (Gộp chi tiết Local & Top 5 Git)
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

                // Biểu đồ Top 5 Phim theo doanh thu (Từ Git)
                var top5Movies = movieTicketStats.OrderByDescending(x => x.Revenue).Take(5).ToList();
                foreach (var mv in top5Movies)
                {
                    model.MovieLabels.Add(mv.MovieTitle ?? "");
                    model.RevenueByMovie.Add(mv.Revenue);
                    model.TicketsByMovie.Add(mv.TicketCount);
                }
            }

            // =====================================================
            // 6. THỐNG KÊ COMBO (Gộp biểu đồ Git & bảng Local)
            // =====================================================
            var comboStats = paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Combo != null)
                .GroupBy(oc => new { oc.Combo.ComboId, oc.Combo.Name })
                .Select(g => new {
                    ComboName = g.Key.Name,
                    QuantitySold = g.Sum(x => x.Quantity ?? 0),
                    Revenue = g.Sum(x => (x.UnitPrice ?? 0) * (x.Quantity ?? 0))
                })
                .OrderByDescending(x => x.QuantitySold)
                .ToList();

            var totalComboSold = comboStats.Sum(x => x.QuantitySold);
            model.ComboRevenue = comboStats.Sum(x => x.Revenue); // Từ Git
            model.ComboSold = totalComboSold; // Từ Git

            // Bảng thống kê
            foreach (var item in comboStats)
            {
                model.ComboStatistics.Add(new ComboStatisticViewModel
                {
                    ComboName = item.ComboName ?? "",
                    QuantitySold = item.QuantitySold,
                    Revenue = item.Revenue,
                    Percentage = totalComboSold == 0 ? 0 : Math.Round(item.QuantitySold * 100.0 / totalComboSold, 2) // Từ Git
                });

            // Combo bán chạy nhất
            var bestCombo = comboStats.FirstOrDefault();

            if (bestCombo != null)
            {
                model.BestSellingCombo =
                    bestCombo.ComboName;

                model.BestSellingQuantity =
                    bestCombo.QuantitySold;
            }
            //có rồi 
            // =====================================================
            // 🔥 KPI COMBO NÂNG CAO
            // =====================================================

            var totalPaidOrders =
                await paidOrders.CountAsync();

            var ordersWithCombo =
                await paidOrders.CountAsync(o =>
                    o.OrderCombos.Any());

            // Pie chart
            foreach (var item in comboStats)
            {
                model.ComboPieLabels.Add(
                    item.ComboName ?? "");

                model.ComboPieValues.Add(
                    item.QuantitySold);
            }

            // =====================================================
            // 7. BIỂU ĐỒ THEO THỜI GIAN (Ngày, Tháng, Năm)
            // =====================================================
            // -- Theo Ngày (Có thêm OrderCount từ Local) --
            var dailyStats = paidOrders
                .GroupBy(x => x.CreatedAt!.Value.Date)
                .Select(g => new {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount ?? 0),
                    Tickets = g.Sum(x => x.Tickets.Count),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var d in dailyStats)
            {
                model.LabelsByDate.Add(d.Date.ToString("dd/MM"));
                model.RevenueByDate.Add(d.Revenue);
                model.TicketCountByDate.Add(d.Tickets);
                model.OrderCountByDate.Add(d.OrderCount);
            }

            // -- Theo Tháng (Từ Git) --
            var monthlyStats = paidOrders
                .GroupBy(x => new { x.CreatedAt!.Value.Year, x.CreatedAt.Value.Month })
                .Select(g => new {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => x.TotalAmount ?? 0),
                    Tickets = g.Sum(x => x.Tickets.Count)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            foreach (var m in monthlyStats)
            {
                model.LabelsByMonth.Add($"{m.Month}/{m.Year}");
                model.RevenueByMonth.Add(m.Revenue);
                model.TicketCountByMonth.Add(m.Tickets);
            }

            // -- Theo Năm (Từ Git) --
            var yearlyStats = paidOrders
                .GroupBy(x => x.CreatedAt!.Value.Year)
                .Select(g => new {
                    Year = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount ?? 0),
                    Tickets = g.Sum(x => x.Tickets.Count)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            yearlyStats.Reverse();
            foreach (var y in yearlyStats)
            {
                model.LabelsByYear.Add(y.Year.ToString());
                model.RevenueByYear.Add(y.Revenue);
                model.TicketCountByYear.Add(y.Tickets);
            }

            // -- Combo theo tháng (Từ Git) --
            var comboByMonth = paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Order != null && oc.Order.CreatedAt != null)
                .GroupBy(oc => new { oc.Order.CreatedAt!.Value.Year, oc.Order.CreatedAt.Value.Month })
                .Select(g => new {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => (x.UnitPrice ?? 0) * (x.Quantity ?? 0)),
                    Quantity = g.Sum(x => x.Quantity ?? 0)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            foreach (var item in comboByMonth)
            {
                model.ComboLabelsByMonth.Add($"{item.Month}/{item.Year}");
                model.ComboRevenueByMonth.Add(item.Revenue);
                model.ComboQuantityByMonth.Add(item.Quantity);
            }

            // =====================================================
            // 8. DOANH THU THEO SUẤT CHIẾU (Từ Git)
            // =====================================================
            var showtimeStats = paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime != null && t.Showtime.Movie != null && t.Showtime.Auditorium != null)
                .GroupBy(t => new {
                    t.ShowtimeId,
                    MovieName = t.Showtime.Movie.Title,
                    AuditoriumName = t.Showtime.Auditorium.Name,
                    StartTime = t.Showtime.StartTime
                })
                .Select(g => new ShowtimeStatisticViewModel
                {
                    ShowtimeId = g.Key.ShowtimeId ?? 0,
                    MovieName = g.Key.MovieName ?? "",
                    AuditoriumName = g.Key.AuditoriumName ?? "",
                    StartTime = g.Key.StartTime ?? DateTime.MinValue,
                    TicketCount = g.Count(),
                    Revenue = g.Sum(x => x.Price ?? 0)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            model.ShowtimeStatistics = showtimeStats;

            foreach (var item in showtimeStats)
            {
                model.ShowtimeLabels.Add($"{item.MovieName} ({item.StartTime:dd/MM HH:mm})");
                model.RevenueByShowtime.Add(item.Revenue);
            }

            // =====================================================
            // 9. THỐNG KÊ THỂ LOẠI & TÌNH TRẠNG PHIM (Từ Local)
            // =====================================================
            var todayDateOnly = DateOnly.FromDateTime(today);
            model.NowShowingCount = await _context.Movies.CountAsync(m => m.IsActive == true && m.ReleaseDate <= todayDateOnly);
            model.ComingSoonCount = await _context.Movies.CountAsync(m => m.IsActive == true && m.ReleaseDate > todayDateOnly);

            var totalActiveMovies = await _context.Movies.CountAsync(m => m.IsActive == true);
            if (totalActiveMovies > 0)
            {
                var genreStats = await _context.Genres
                    .Select(g => new {
                        g.Name,
                        MovieCount = _context.Movies.Count(m => m.IsActive == true && m.Genres.Any(genre => genre.GenreId == g.GenreId))
                    })
                    .Where(x => x.MovieCount > 0)
                    .OrderByDescending(x => x.MovieCount)
                    .ToListAsync();

                foreach (var item in genreStats)
                {
                    model.GenreStatistics.Add(new GenreStatisticViewModel
                    {
                        GenreName = item.Name ?? "",
                        MovieCount = item.MovieCount,
                        Percentage = Math.Round(item.MovieCount * 100.0 / totalActiveMovies, 2)
                    });
                }
            }

            return model;
        }

        // =====================================================
        // CÁC CHỨC NĂNG NÂNG CAO & API TỪ GIT 
        // =====================================================

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

            var advShowtimeStats = await query2.OrderByDescending(t => t.Showtime.StartTime).Take(10)
                                            .GroupBy(t => new { t.Showtime.Movie.Title, t.Showtime.StartTime })
                                            .Select(g => new { Info = $"{g.Key.Title} ({g.Key.StartTime:HH:mm})", Count = g.Count() }).ToListAsync();

            model.ShowtimeLabels = advShowtimeStats.Select(x => x.Info).ToList();
            model.TicketsByShowtime = advShowtimeStats.Select(x => x.Count).ToList();

            ViewBag.Theaters = await _context.Theaters.ToListAsync();
            ViewBag.Movies = await _context.Movies.ToListAsync();

            var totalSeats = await _context.Seats.CountAsync();
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
        public async Task<JsonResult> GetMovieTicketData(int? mId)
        {
            var query = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán") && t.Showtime != null);

            if (mId.HasValue && mId > 0)
                query = query.Where(t => t.Showtime.MovieId == mId.Value);

            var data = await query.GroupBy(t => t.Showtime.Movie.Title)
                                  .Select(g => new { Name = g.Key, Count = g.Count() })
                                  .OrderByDescending(x => x.Count)
                                  .Take(10)
                                  .ToListAsync();

            return Json(new { labels = data.Select(x => x.Name), values = data.Select(x => x.Count) });
        }

        [HttpGet]
        public async Task<JsonResult> GetShowtimeTicketData(int? mId, int? tId2)
        {
            var showtimesQuery = _context.Showtimes
                .Include(s => s.Movie)
                .AsQueryable();

            if (mId.HasValue && mId > 0) showtimesQuery = showtimesQuery.Where(s => s.MovieId == mId.Value);
            if (tId2.HasValue && tId2 > 0) showtimesQuery = showtimesQuery.Where(s => s.Auditorium.TheaterId == tId2.Value);

            var showtimes = await showtimesQuery.OrderBy(s => s.StartTime).Take(10).ToListAsync();

            var tickets = await _context.Tickets
                .Where(t => t.Order.Status.Contains("thanh toán"))
                .GroupBy(t => t.ShowtimeId)
                .Select(g => new { ShowtimeId = g.Key, Count = g.Count() })
                .ToListAsync();

            var data = showtimes.Select(s => new {
                Title = s.Movie.Title,
                Time = s.StartTime?.ToString("HH:mm") ?? "--:--",
                RawTime = s.StartTime,
                Count = tickets.FirstOrDefault(t => t.ShowtimeId == s.ShowtimeId)?.Count ?? 0
            }).ToList();

            return Json(new
            {
                labels = data.Select(x => $"{x.Title} ({x.Time})").ToList(),
                values = data.Select(x => x.Count).ToList()
            });
        }
    }
}