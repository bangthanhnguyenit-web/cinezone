using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CINEMA.Controllers;

namespace CINEMA.ViewComponents
{
    public class RevenueDashboardViewComponent : ViewComponent
    {
        private readonly StatisticsController _statisticsController;

        // Tiêm trực tiếp StatisticsController vào qua DI (Cách của máy bạn)
        public RevenueDashboardViewComponent(StatisticsController statisticsController)
        {
            _statisticsController = statisticsController;
        }

        public async Task<IViewComponentResult> InvokeAsync(DateTime? from, DateTime? to)
        {
            // Gọi hàm xử lý dashboard từ controller đã gộp (Logic của Git)
            var model = await _statisticsController.BuildDashboard(from, to);

            return View(model);
        }
    }
}