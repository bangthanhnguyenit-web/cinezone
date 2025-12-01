using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CINEMA.Controllers;
using CINEMA.Models;

namespace CINEMA.ViewComponents
{
    public class RevenueDashboardViewComponent : ViewComponent
    {
        private readonly CinemaContext _context;

        public RevenueDashboardViewComponent(CinemaContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(DateTime? from, DateTime? to)
        {
            // Gọi chung hàm thống kê trong StatisticsController
            var controller = new StatisticsController(_context);
            var model = await controller.BuildDashboard(from, to);

            return View(model);
        }
    }
}
