using CINEMA.Models;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace CINEMA.Controllers
{
    public class CustomerController : Controller
    {
        private readonly CinemaContext _context;

        public CustomerController(CinemaContext context)
        {
            _context = context;
        }

        // ------------------ 🟢 ĐĂNG KÝ ------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Kiểm tra email trùng
            var exist = _context.Customers.FirstOrDefault(c => c.Email == model.Email);
            if (exist != null)
            {
                ViewBag.Error = "Email đã tồn tại!";
                return View(model);
            }

            // Tạo mới khách hàng
            var customer = new Customer
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                CreatedAt = DateTime.Now,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
            };

            _context.Customers.Add(customer);
            _context.SaveChanges();

            // Sau khi đăng ký → về trang Login
            TempData["Success"] = "Đăng ký thành công! Hãy đăng nhập để tiếp tục.";
            return RedirectToAction("Login", "Customer");
        }

        // ------------------ 🟢 ĐĂNG NHẬP ------------------
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Giữ returnUrl để sau đăng nhập xong quay lại trang trước
            var model = new LoginViewModel { ReturnUrl = returnUrl ?? Url.Action("Index", "Home") };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View(model);
            }

            // Tìm khách hàng
            var customer = _context.Customers.FirstOrDefault(c => c.Email == model.Email);

            if (customer == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
                return View(model);
            }

            bool checkPassword = false;

            try
            {
                checkPassword = BCrypt.Net.BCrypt.Verify(
                    model.Password,
                    customer.PasswordHash
                );
            }
            catch
            {
                ViewBag.Error = "Tài khoản này đăng nhập bằng Google!";
                return View(model);
            }

            if (!checkPassword)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
                return View(model);
            }

            // 🟩 Lưu thông tin session
            HttpContext.Session.SetInt32("CustomerId", customer.CustomerId);
            HttpContext.Session.SetString("CustomerName", customer.FullName);
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            // 🟩 Điều hướng
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            else
                return RedirectToAction("Index", "Home");
        }

        // ------------------ 🟢 QUÊN MẬT KHẨU ------------------
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Vui lòng nhập email.";
                return View();
            }

            var customer = _context.Customers.FirstOrDefault(c => c.Email == email);
            if (customer == null)
            {
                ViewBag.Message = $"Nếu email {email} tồn tại, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";
                return View();
            }

            ViewBag.Message = $"Hướng dẫn đặt lại mật khẩu đã được gửi đến {email}.";
            return View();
        }

        // ------------------ 🟢 ĐĂNG XUẤT ------------------
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Customer");
        }

        // ------------------ 🟢 HỒ SƠ CÁ NHÂN ------------------
        [HttpGet]
        public IActionResult Profile()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var customer = _context.Customers.FirstOrDefault(c => c.CustomerId == customerId);
            if (customer == null)
                return RedirectToAction("Login", "Customer");

            return View(customer);
        }
        [HttpGet]
        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("CustomerId");
            var customer = _context.Customers.Find(userId);
            return View(customer);
        }
        [HttpPost]
        public IActionResult EditProfile(Customer model, IFormFile avatarFile)
        {
            var userId = HttpContext.Session.GetInt32("CustomerId");
            var customer = _context.Customers.Find(userId);

            if (customer == null) return NotFound();

            customer.FullName = model.FullName;
            customer.Phone = model.Phone;

            if (avatarFile != null && avatarFile.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    avatarFile.CopyTo(stream);
                }

                customer.Avatar = "/images/" + fileName;
            }

            _context.SaveChanges();

            return RedirectToAction("Profile");
        }
        // ================= LOGIN GOOGLE =================

        // ================= LOGIN GOOGLE =================

        public IActionResult LoginGoogle(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(
                "GoogleResponse",
                "Customer",
                new { returnUrl }
            );

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync();

            if (!result.Succeeded || result.Principal == null)
            {
                return RedirectToAction("Login");
            }

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(x => x.Email == email);

            if (customer == null)
            {
                customer = new Customer
                {
                    Email = email,
                    FullName = name ?? email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    CreatedAt = DateTime.Now
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.SetInt32("CustomerId", customer.CustomerId);
            HttpContext.Session.SetString("CustomerName", customer.FullName);
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            if (!string.IsNullOrEmpty(returnUrl)
                && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
