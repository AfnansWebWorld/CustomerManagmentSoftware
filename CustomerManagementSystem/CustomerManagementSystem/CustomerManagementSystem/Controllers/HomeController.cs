using System.Diagnostics;
using CustomerManagementSystem.Data;
using CustomerManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace CustomerManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var dashboardData = new DashboardViewModel
            {
                TotalCustomers = await _context.Customers.CountAsync(),
                ActiveCustomers = await _context.Customers.CountAsync(c => c.IsActive),
                InactiveCustomers = await _context.Customers.CountAsync(c => !c.IsActive),
                RecentCustomers = await _context.Customers
                    .OrderByDescending(c => c.RegistrationDate)
                    .Take(5)
                    .Include(c => c.Category)
                    .ToListAsync()
            };

            // Prepare data for the chart (last 7 days)
            var date = DateTime.Now.Date;
            for (int i = 6; i >= 0; i--)
            {
                var currentDate = date.AddDays(-i);
                dashboardData.RegistrationDates.Add(currentDate.ToShortDateString());
                dashboardData.CustomersCount.Add(_context.Customers
                    .Count(c => c.RegistrationDate.Date == currentDate));
            }

            return View(dashboardData);
        }
    }

  
}

