using System.Diagnostics;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CinemaContext _context;

        public HomeController(ILogger<HomeController> logger, CinemaContext context)
        {
            _logger = logger;
            _context = context;
        }

        // ===============================================================
        // ============================ TRANG CHỦ =========================
        // ===============================================================

        public IActionResult Index()
        {
            // Tất cả phim đang chiếu
            var movies = _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            // Hôm nay (DateOnly để so sánh với ReleaseDate)
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Phim sắp chiếu
            var comingSoon = _context.Movies
                .Where(m => m.IsActive == true
                            && m.ReleaseDate.HasValue
                            && m.ReleaseDate.Value > today)
                .OrderBy(m => m.ReleaseDate)
                .ToList();

            // Danh sách rạp cho dropdown "Đặt vé nhanh"
            var theaters = _context.Theaters
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.Name)
                .ToList();

            ViewBag.ComingSoon = comingSoon;
            ViewBag.Theaters = theaters;

            return View(movies);
        }


        // ===============================================================
        // ====================== API CHỌN RẠP / PHIM / SUẤT ==============
        // ===============================================================

        // 🔹 Lấy danh sách phim theo rạp
        [HttpGet]
        public IActionResult GetMoviesByTheater(int theaterId)
        {
            var movies = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(s => s.Auditorium.TheaterId == theaterId
                            && s.IsActive == true
                            && s.Movie.IsActive == true
                            && s.StartTime.HasValue
                            && s.StartTime.Value >= DateTime.Now)
                .Select(s => new
                {
                    s.Movie.MovieId,
                    s.Movie.Title
                })
                .Distinct()
                .OrderBy(m => m.Title)
                .ToList();

            return Json(movies);
        }

        // 🔹 Lấy suất chiếu theo rạp + phim
        [HttpGet]
        public IActionResult GetShowtimes(int theaterId, int movieId)
        {
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(s => s.Auditorium.TheaterId == theaterId
                            && s.MovieId == movieId
                            && s.IsActive == true
                            && s.StartTime.HasValue
                            && s.StartTime.Value >= DateTime.Now)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.ShowtimeId,
                    Date = s.StartTime!.Value.ToString("yyyy-MM-dd"),
                    Time = s.StartTime!.Value.ToString("HH:mm"),
                    Price = s.BasePrice ?? 0
                })
                .ToList();

            return Json(showtimes);
        }


        // ===============================================================
        // ============================ ĐẶT VÉ =============================
        // ===============================================================

        // 🔹 GET: Trang chọn ghế
        [HttpGet]
        public IActionResult BookTicket(int id, int? showtimeId)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id && m.IsActive == true);

            if (movie == null)
                return NotFound("Không tìm thấy phim.");

            // Nếu chưa chọn suất → chọn suất sớm nhất
            if (!showtimeId.HasValue)
            {
                showtimeId = _context.Showtimes
                    .Where(s => s.MovieId == id
                                && s.IsActive == true
                                && s.StartTime.HasValue
                                && s.StartTime.Value >= DateTime.Now)
                    .OrderBy(s => s.StartTime)
                    .Select(s => s.ShowtimeId)
                    .FirstOrDefault();
            }

            Showtime? showtime = null;

            if (showtimeId.HasValue && showtimeId.Value != 0)
            {
                showtime = _context.Showtimes
                    .Include(s => s.Auditorium)
                        .ThenInclude(a => a.Theater)
                    .FirstOrDefault(s => s.ShowtimeId == showtimeId.Value
                                         && s.IsActive == true);
            }

            // Tải ghế
            var seats = new List<Seat>();
            if (showtime?.AuditoriumId != null)
            {
                seats = _context.Seats
                    .Where(s => s.AuditoriumId == showtime.AuditoriumId
                                && s.IsActive == true)
                    .OrderBy(s => s.RowLabel)
                    .ThenBy(s => s.SeatNumber)
                    .ToList();
            }

            // Ghế đã đặt
            var bookedSeats = new List<string>();
            if (showtime != null)
            {
                bookedSeats = _context.Tickets
                    .Where(t => t.ShowtimeId == showtime.ShowtimeId)
                    .Include(t => t.Seat)
                    .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                    .ToList();
            }

            // Suất chiếu
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(s => s.MovieId == id
                            && s.IsActive == true
                            && s.StartTime.HasValue
                            && s.StartTime.Value >= DateTime.Now)
                .OrderBy(s => s.StartTime)
                .ToList();

            // Combo
            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            ViewBag.Showtime = showtime;
            ViewBag.Showtimes = showtimes;
            ViewBag.Combos = combos;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;

            return View(movie);
        }

        // 🔹 POST: Đặt vé nhanh → chuyển đến trang chọn ghế
        [HttpPost]
        public IActionResult BookTicket(int movieId, int showtimeId)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == movieId && m.IsActive == true);

            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId && s.IsActive == true);

            if (movie == null || showtime == null)
                return NotFound("Không tìm thấy phim hoặc suất chiếu.");

            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            var seats = _context.Seats
                .Where(s => s.AuditoriumId == showtime.AuditoriumId
                            && s.IsActive == true)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();

            var bookedSeats = _context.Tickets
                .Where(t => t.ShowtimeId == showtimeId)
                .Include(t => t.Seat)
                .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                .ToList();

            ViewBag.Showtime = showtime;
            ViewBag.Combos = combos;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;

            return View(movie);
        }


        // ===============================================================
        // ============================ LỊCH CHIẾU =========================
        // ===============================================================

        [HttpGet]
        public IActionResult Schedule(DateTime? date)
        {
            var selectedDate = date?.Date ?? DateTime.Today;

            var movies = _context.Movies
                .Include(m => m.Genres)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(m => m.IsActive == true
                            && m.Showtimes.Any(s =>
                                   s.StartTime.HasValue
                                   && s.StartTime.Value.Date == selectedDate))
                .OrderBy(m => m.Title)
                .ToList();

            ViewBag.SelectedDate = selectedDate;
            return View(movies);
        }


        // ===============================================================
        // ============================ THANH TOÁN ========================
        // ===============================================================

        [HttpPost]
        public IActionResult GoToPayment(int movieId, int showtimeId, string selectedSeats, int comboId)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == movieId);
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);

            var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);

            if (movie == null || showtime == null)
                return NotFound("Phim hoặc suất chiếu không tồn tại.");

            int seatCount = selectedSeats
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

            decimal ticketPrice = (showtime.BasePrice ?? 0) * seatCount;
            decimal comboPrice = combo?.Price ?? 0;
            decimal total = ticketPrice + comboPrice;

            ViewBag.Movie = movie;
            ViewBag.Showtime = showtime;
            ViewBag.SelectedSeats = selectedSeats;
            ViewBag.Combo = combo;
            ViewBag.Total = total;

            return View("Payment");
        }


        // ===============================================================
        // ======================== PRIVACY / ERROR =======================
        // ===============================================================

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
