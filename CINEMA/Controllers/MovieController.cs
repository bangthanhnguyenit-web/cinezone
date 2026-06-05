using CINEMA.Controllers;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class MovieController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public MovieController(CinemaContext context)
        {
            _context = context;
        }

        // ==================== DANH SÁCH PHIM ====================
        public IActionResult Index()
        {
            var movies = _context.Movies
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            return View(movies);
        }

        // ==================== CHI TIẾT PHIM ====================
        public IActionResult Details(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // ==================== THÊM PHIM (GET) ====================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // ==================== THÊM PHIM (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Movie movie, IFormFile? PosterImage)
        {
            if (!ModelState.IsValid)
                return View(movie);

            // Upload ảnh
            if (PosterImage != null && PosterImage.Length > 0)
            {
                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "movies");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = Path.GetFileName(PosterImage.FileName);
                var filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    PosterImage.CopyTo(stream);
                }

                movie.PosterUrl = "/images/movies/" + fileName;
            }

            movie.IsActive = true; // Luôn active khi thêm mới

            _context.Movies.Add(movie);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "🎉 Thêm phim thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== SỬA PHIM (GET) ====================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // ==================== SỬA PHIM (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Movie updatedMovie, IFormFile? PosterImage)
        {
            if (!ModelState.IsValid)
                return View(updatedMovie);

            // Lấy bản gốc từ DB để tránh lỗi tracking
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == updatedMovie.MovieId);
            if (movie == null) return NotFound();

            // Cập nhật dữ liệu
            movie.Title = updatedMovie.Title;
            movie.Description = updatedMovie.Description;
            movie.ReleaseDate = updatedMovie.ReleaseDate;
            movie.Duration = updatedMovie.Duration;
            movie.IsActive = updatedMovie.IsActive; // ⭐ CHỖ QUAN TRỌNG NHẤT

            // Upload ảnh nếu có
            if (PosterImage != null && PosterImage.Length > 0)
            {
                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "movies");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = Path.GetFileName(PosterImage.FileName);
                var filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    PosterImage.CopyTo(stream);
                }

                movie.PosterUrl = "/images/movies/" + fileName;
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = "✏️ Cập nhật phim thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== XÓA PHIM (GET) ====================
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // ==================== XÓA PHIM (POST) ====================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var movie = _context.Movies
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Tickets)
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null) return NotFound();

            // Xóa vé + suất chiếu
            if (movie.Showtimes != null)
            {
                foreach (var show in movie.Showtimes)
                {
                    if (show.Tickets?.Any() == true)
                        _context.Tickets.RemoveRange(show.Tickets);
                }

                _context.Showtimes.RemoveRange(movie.Showtimes);
            }

            _context.Movies.Remove(movie);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"🗑️ Đã xóa phim \"{movie.Title}\" cùng toàn bộ suất chiếu!";
            return RedirectToAction(nameof(Index));
        }
    }
}
