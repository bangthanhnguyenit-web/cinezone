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

        // =====================================================
        // ====================== TRANG CHỦ ====================
        // =====================================================

        public IActionResult Index()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Phim đang chiếu
            var movies = _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            // Phim sắp chiếu
            var comingSoon = _context.Movies
                .Where(m => m.IsActive == true &&
                            m.ReleaseDate.HasValue &&
                            m.ReleaseDate > today)
                .OrderBy(m => m.ReleaseDate)
                .ToList();

            // Rạp
            var theaters = _context.Theaters
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.Name)
                .ToList();

            // Popup quảng cáo
            var popup = _context.Popups
                .FirstOrDefault(p => p.IsActive == true);

            ViewBag.ComingSoon = comingSoon;
            ViewBag.Theaters = theaters;
            ViewBag.Popup = popup;

            return View(movies);
        }

        // =====================================================
        // ================== API QUICK BOOKING =================
        // =====================================================

        // Lấy phim theo rạp
        [HttpGet]
        public IActionResult GetMoviesByTheater(int theaterId)
        {
            var movies = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .Where(s =>
                       s.Auditorium.TheaterId == theaterId &&
                       s.IsActive == true &&
                       s.Movie.IsActive == true &&
                       s.StartTime >= DateTime.Now)
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

        // Lấy suất chiếu
        [HttpGet]
        public IActionResult GetShowtimes(int theaterId, int movieId)
        {
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                .Where(s =>
                       s.Auditorium.TheaterId == theaterId &&
                       s.MovieId == movieId &&
                       s.IsActive == true &&
                       s.StartTime >= DateTime.Now)
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

        // =====================================================
        // ======================= ĐẶT VÉ =======================
        // =====================================================

        [HttpGet]
        public IActionResult BookTicket(int id, int? showtimeId)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id && m.IsActive == true);

            if (movie == null)
                return NotFound("Không tìm thấy phim.");

            // Nếu chưa chọn suất → lấy suất sớm nhất
            if (!showtimeId.HasValue)
            {
                showtimeId = _context.Showtimes
                    .Where(s =>
                        s.MovieId == id &&
                        s.IsActive == true &&
                        s.StartTime >= DateTime.Now)
                    .OrderBy(s => s.StartTime)
                    .Select(s => s.ShowtimeId)
                    .FirstOrDefault();
            }

            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId && s.IsActive == true);

            // Ghế
            var seats = _context.Seats
                .Where(s => s.AuditoriumId == showtime.AuditoriumId && s.IsActive == true)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();

            // Ghế đã đặt
            var bookedSeats = _context.Tickets
                .Where(t => t.ShowtimeId == showtime.ShowtimeId)
                .Include(t => t.Seat)
                .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                .ToList();

            // Suất chiếu
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(s =>
                       s.MovieId == id &&
                       s.IsActive == true &&
                       s.StartTime >= DateTime.Now)
                .OrderBy(s => s.StartTime)
                .ToList();

            // Combo
            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            ViewBag.Showtime = showtime;
            ViewBag.Showtimes = showtimes;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;
            ViewBag.Combos = combos;

            return View(movie);
        }

        // POST từ Quick Booking
        [HttpPost]
        public IActionResult BookTicket(int movieId, int showtimeId)
        {
            return RedirectToAction("BookTicket", new
            {
                id = movieId,
                showtimeId = showtimeId
            });
        }

        // =====================================================
        // ======================= LỊCH CHIẾU ===================
        // =====================================================

        [HttpGet]
        public IActionResult Schedule(DateTime? date)
        {
            var selectedDate = date?.Date ?? DateTime.Today;

            var movies = _context.Movies
                .Include(m => m.Genres)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(m =>
                       m.IsActive == true &&
                       m.Showtimes.Any(s =>
                           s.StartTime.HasValue &&
                           s.StartTime.Value.Date == selectedDate))
                .OrderBy(m => m.Title)
                .ToList();

            ViewBag.SelectedDate = selectedDate;

            return View(movies);
        }

        // =====================================================
        // ======================= THANH TOÁN ===================
        // =====================================================

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
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
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

        // =====================================================
        // =================== PRIVACY / ERROR ==================
        // =====================================================

        public IActionResult Privacy()
        {
            return View();
        }

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