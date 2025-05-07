using CustomerManagementSystem.Data;
using CustomerManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;   
using System.IO;
using System.Data;
using Microsoft.Reporting.WinForms;
using System.Xml.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CustomerManagementSystem.Controllers
{
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ApplicationDbContext context, ILogger<CustomersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Customers
        public async Task<IActionResult> Index()
        {
            return View(await _context.Customers.Include(c => c.Category).ToListAsync());
        }

        // GET: Customers/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesViewBag();
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {

                // Server-side validation
                if (string.IsNullOrWhiteSpace(customer.Name))
                {
                    ModelState.AddModelError(nameof(customer.Name), "Name is required");
                    await PopulateCategoriesViewBag();
                    return View(customer);
                }

                // Set server-side values
                customer.RegistrationDate = DateTime.Now;

                // Verify category exists
                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == customer.CategoryId);

                if (!categoryExists)
                {
                    ModelState.AddModelError(nameof(customer.CategoryId), "Invalid category selected");
                    await PopulateCategoriesViewBag();
                    return View(customer);
                }

                // Begin transaction for data integrity
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _context.Customers.Add(customer);
                    int recordsAffected = await _context.SaveChangesAsync();

                    if (recordsAffected == 0)
                    {
                        throw new DbUpdateException("No records were affected");
                    }

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Customer created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating customer");
                ModelState.AddModelError("", "A database error occurred. Please try again.");

                // Inspect inner exception for more details
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception details");

                    // Handle specific SQL Server errors
                    if (ex.InnerException.Message.Contains("IX_Customers_Phone"))
                    {
                        ModelState.AddModelError(nameof(customer.Phone), "This phone number already exists");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating customer");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            // If we get here, something went wrong
            await PopulateCategoriesViewBag();
            return View(customer);
        }
        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit attempted without ID");
                return NotFound();
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
            {
                _logger.LogWarning("Customer not found for ID {CustomerId}", id);
                return NotFound();
            }

            await PopulateCategoriesViewBag();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                _logger.LogWarning("ID mismatch in edit - Route: {RouteId}, Model: {ModelId}", id, customer.Id);
                return NotFound();
            }



            // Begin transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get existing customer to preserve audit fields
                var existingCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (existingCustomer == null)
                {
                    _logger.LogWarning("Customer not found during edit for ID {CustomerId}", id);
                    return NotFound();
                }

                // Preserve original registration date
                customer.RegistrationDate = existingCustomer.RegistrationDate;

                // Verify category exists
                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == customer.CategoryId);

                if (!categoryExists)
                {
                    ModelState.AddModelError(nameof(customer.CategoryId), "Invalid category selected");
                    await PopulateCategoriesViewBag();
                    return View(customer);
                }

                _context.Update(customer);
                var changes = await _context.SaveChangesAsync();

                if (changes > 0)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation("Customer {CustomerId} updated successfully", id);
                    TempData["SuccessMessage"] = "Customer updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("No changes saved for customer {CustomerId}", id);
                ModelState.AddModelError("", "No changes were saved.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();

                if (!CustomerExists(customer.Id))
                {
                    _logger.LogWarning("Concurrency error - customer not found");
                    return NotFound();
                }

                _logger.LogError(ex, "Concurrency error updating customer {CustomerId}", id);
                ModelState.AddModelError("", "The record was modified by another user. Please refresh and try again.");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error updating customer {CustomerId}", id);

                if (ex.InnerException?.Message.Contains("IX_Customers_Phone") == true)
                {
                    ModelState.AddModelError(nameof(customer.Phone), "This phone number already exists");
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error updating customer {CustomerId}", id);
                ModelState.AddModelError("", "An unexpected error occurred.");
            }

            await PopulateCategoriesViewBag();
            return View(customer);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null) return NotFound();

            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null) return NotFound();

            return View(customer);
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }

        private async Task PopulateCategoriesViewBag()
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
        }
        [HttpGet]
        [ActionName("Report")]
        
        public IActionResult GenerateReport()
        {
            // Return the view with the form
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> GenerateReport(
    DateTime? startDate,
    DateTime? endDate,
    bool includeInactive = true,
    int? categoryId = null)
        {
            try
            {
                // Validate date range
                if (!startDate.HasValue) startDate = DateTime.MinValue;
                if (!endDate.HasValue) endDate = DateTime.MaxValue;

                if (startDate > endDate)
                {
                    TempData["ErrorMessage"] = "End date must be after start date";
                    return RedirectToAction("Index");
                }

                // Get customers from database with related Category data
                var customers = await GetCustomersFromDatabase(
                    startDate.Value,
                    endDate.Value,
                    includeInactive,
                    categoryId);

                // Generate PDF
                using (var ms = new MemoryStream())
                {
                    var document = new Document(PageSize.A4.Rotate()); // Landscape orientation
                    var writer = PdfWriter.GetInstance(document, ms);

                    document.Open();

                    // Add report header
                    var header = new Paragraph("CUSTOMER DETAILS REPORT",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18));
                    header.Alignment = Element.ALIGN_CENTER;
                    document.Add(header);

                    // Add filter information
                    var filters = new Paragraph();
                    filters.Add(new Chunk("Date Range: ", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                    filters.Add(new Chunk($"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}\n", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
                    filters.Add(new Chunk("Included Inactive: ", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                    filters.Add(new Chunk($"{(includeInactive ? "Yes" : "No")}\n", FontFactory.GetFont(FontFactory.HELVETICA, 10)));

                    if (categoryId.HasValue)
                    {
                        var categoryName = customers.FirstOrDefault()?.Category?.Name ?? "N/A";
                        filters.Add(new Chunk("Category: ", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                        filters.Add(new Chunk($"{categoryName}", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
                    }

                    document.Add(filters);
                    document.Add(Chunk.NEWLINE);

                    // Create table with all customer properties
                    var table = new PdfPTable(8)
                    {
                        WidthPercentage = 100,
                        SpacingBefore = 10f,
                        HorizontalAlignment = Element.ALIGN_LEFT
                    };

                    // Set column widths
                    float[] columnWidths = { 0.5f, 2f, 1.5f, 2f, 2f, 1.5f, 1f, 1.5f };
                    table.SetWidths(columnWidths);

                    // Add headers
                    table.AddCell(CreateHeaderCell("ID"));
                    table.AddCell(CreateHeaderCell("Full Name"));
                    table.AddCell(CreateHeaderCell("Phone"));
                    table.AddCell(CreateHeaderCell("Address"));
                    table.AddCell(CreateHeaderCell("Business"));
                    table.AddCell(CreateHeaderCell("Category"));
                    table.AddCell(CreateHeaderCell("Active"));
                    table.AddCell(CreateHeaderCell("Reg. Date"));

                    // Add data rows
                    foreach (var customer in customers)
                    {
                        table.AddCell(CreateStandardCell(customer.Id.ToString()));
                        table.AddCell(CreateStandardCell(customer.Name));
                        table.AddCell(CreateStandardCell(customer.Phone));
                        table.AddCell(CreateStandardCell(customer.Address));
                        table.AddCell(CreateStandardCell(customer.BusinessName ?? "N/A"));
                        table.AddCell(CreateStandardCell(customer.Category?.Name ?? "N/A"));
                        table.AddCell(CreateStatusCell(customer.IsActive));
                        table.AddCell(CreateStandardCell(customer.RegistrationDate.ToString("yyyy-MM-dd")));
                    }

                    document.Add(table);

                    // Add footer
                    var footer = new Paragraph(
                        $"Generated on {DateTime.Now:yyyy-MM-dd HH:mm} | Total Records: {customers.Count}",
                        FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8));
                    footer.Alignment = Element.ALIGN_RIGHT;
                    document.Add(footer);

                    document.Close();

                    return File(ms.ToArray(), "application/pdf",
                        $"CustomerReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer report");
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return RedirectToAction("Index");
            }
        }

        private async Task<List<Customer>> GetCustomersFromDatabase(
            DateTime startDate,
            DateTime endDate,
            bool includeInactive,
            int? categoryId)
        {
            var query = _context.Customers
                .Include(c => c.Category) // Include related Category
                .Where(c => c.RegistrationDate >= startDate && c.RegistrationDate <= endDate);

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(c => c.CategoryId == categoryId.Value);
            }

            return await query
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        private PdfPCell CreateHeaderCell(string text)
        {
            return new PdfPCell(new Phrase(text,
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)))
            {
                BackgroundColor = new BaseColor(220, 220, 220),
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 5,
                PaddingTop = 7
            };
        }

        private PdfPCell CreateStandardCell(string text)
        {
            return new PdfPCell(new Phrase(text,
                FontFactory.GetFont(FontFactory.HELVETICA, 10)))
            {
                Padding = 5
            };
        }

        private PdfPCell CreateStatusCell(bool isActive)
        {
            return new PdfPCell(new Phrase(isActive ? "✓" : "✗",
                FontFactory.GetFont(FontFactory.HELVETICA, 10)))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                BackgroundColor = isActive ? new BaseColor(220, 255, 220) : new BaseColor(255, 220, 220),
                Padding = 5
            };
        }
    }
    }