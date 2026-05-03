using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CINEMA.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly CinemaContext _context;
        private readonly GeminiService _gemini;

        public ChatbotController(CinemaContext context, GeminiService gemini)
        {
            _context = context;
            _gemini = gemini;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return Json("🤖 Bạn hãy nhập câu hỏi nhé!");

            var msg = RemoveVietnameseTone(req.Message.ToLower());

            // =====================================================
            // 🎬 1. TÌM PHIM THEO TÊN
            // =====================================================
            var movie = _context.Movies
                .AsEnumerable()
                .FirstOrDefault(m =>
                {
                    var title = RemoveVietnameseTone(m.Title.ToLower());
                    var words = msg.Split(' ');
                    return (m.IsActive ?? false) && words.Any(w => title.Contains(w));
                });

            if (movie != null)
            {
                var shows = _context.Showtimes
                    .Where(s =>
                        s.MovieId == movie.MovieId &&
                        (s.IsActive ?? false) &&
                        s.StartTime >= DateTime.Now)
                    .OrderBy(s => s.StartTime)
                    .Take(5)
                    .ToList();

                if (!shows.Any())
                    return Json($"😢 Phim '<b>{movie.Title}</b>' chưa có suất chiếu");

                var result = shows.Select(s =>
                    $"<div style='margin-bottom:10px'>" +
                    $"🎬 <b>{movie.Title}</b><br>" +
                    $"⏰ {s.StartTime:HH:mm dd/MM}<br>" +
                    $"<a href='/Home/BookTicket?id={movie.MovieId}&showtimeId={s.ShowtimeId}' " +
                    $"style='color:#198754;font-weight:bold'>🎟 Đặt vé</a>" +
                    $"</div>"
                );

                return Json(string.Join("", result));
            }

            // =====================================================
            // 🎬 2. PHIM ĐANG CHIẾU
            // =====================================================
            if (msg.Contains("dang chieu"))
            {
                var movies = _context.Movies
                    .Where(m => m.IsActive ?? false)
                    .Select(m => m.Title)
                    .Take(10)
                    .ToList();

                return Json("🎬 Phim đang chiếu:<br>- " + string.Join("<br>- ", movies));
            }

            // =====================================================
            // ⏰ 3. LỊCH CHIẾU
            // =====================================================
            if (msg.Contains("lich"))
            {
                var shows = _context.Showtimes
                    .Include(s => s.Movie)
                    .Where(s =>
                        (s.IsActive ?? false) &&
                        s.StartTime >= DateTime.Now)
                    .OrderBy(s => s.StartTime)
                    .Take(5)
                    .ToList();

                var result = shows.Select(s =>
                    $"<div style='margin-bottom:10px'>" +
                    $"🎬 <b>{s.Movie.Title}</b><br>" +
                    $"⏰ {s.StartTime:HH:mm dd/MM}<br>" +
                    $"<a href='/Home/BookTicket?id={s.MovieId}&showtimeId={s.ShowtimeId}' " +
                    $"style='color:#198754;font-weight:bold'>🎟 Đặt vé</a>" +
                    $"</div>"
                );

                return Json("⏰ Lịch chiếu:<br>" + string.Join("<br><br>", result));
            }

            // =====================================================
            // 💺 4. GHẾ TRỐNG
            // =====================================================
            if (msg.Contains("ghe"))
            {
                int totalSeats = _context.Seats.Count();
                int booked = _context.Tickets.Count();
                int available = totalSeats - booked;

                return Json($"💺 Còn khoảng <b>{available}</b> ghế trống");
            }

            // =====================================================
            // 🤖 5. GEMINI (AI)
            // =====================================================
            try
            {
                var ai = await _gemini.Ask(req.Message);

                Console.WriteLine(ai); // debug

                dynamic json = JsonConvert.DeserializeObject(ai);

                string text = "⚠️ AI chưa phản hồi";

                if (json?.candidates != null &&
                    json.candidates.Count > 0 &&
                    json.candidates[0].content != null &&
                    json.candidates[0].content.parts != null &&
                    json.candidates[0].content.parts.Count > 0)
                {
                    text = json.candidates[0].content.parts[0].text;
                }
                Console.WriteLine(ai);
                return Json("🤖 " + text);
            }
            catch
            {
                return Json("❌ Lỗi AI, thử lại sau");
            }

        }

        // =====================================================
        // 🔥 REMOVE DẤU TIẾNG VIỆT
        // =====================================================
        public static string RemoveVietnameseTone(string text)
        {
            string[] arr1 = {
                "á","à","ả","ã","ạ","ă","ắ","ằ","ẳ","ẵ","ặ","â","ấ","ầ","ẩ","ẫ","ậ",
                "đ",
                "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
                "í","ì","ỉ","ĩ","ị",
                "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
                "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
                "ý","ỳ","ỷ","ỹ","ỵ"
            };

            string[] arr2 = {
                "a","a","a","a","a","a","a","a","a","a","a","a","a","a","a","a","a",
                "d",
                "e","e","e","e","e","e","e","e","e","e","e",
                "i","i","i","i","i",
                "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
                "u","u","u","u","u","u","u","u","u","u","u",
                "y","y","y","y","y"
            };

            for (int i = 0; i < arr1.Length; i++)
            {
                text = text.Replace(arr1[i], arr2[i]);
            }

            return text;
        }
    }
}