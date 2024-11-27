using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DemoMvcApplication.Data;
using DemoMvcApplication.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DemoMvcApplication.Controllers
{
    [Route("[controller]")]
    public class EmployeeController : Controller
    {
        private readonly ILogger<EmployeeController> _logger;
        private readonly EmpDbContext _empDbContext;

        public EmployeeController(ILogger<EmployeeController> logger, EmpDbContext empDbContext)
        {
            _logger = logger;
            _empDbContext = empDbContext;
        }

        [HttpGet]
        public IActionResult Index(string searchTerm, int pageNumber = 1)
        {
            //return View(_empDbContext.employees.ToList());
            var query = _empDbContext.employees.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e => e.Name.Contains(searchTerm) ||
                                 e.Gender.Contains(searchTerm) ||
                                 e.Place.Contains(searchTerm));
            }


            var totalEmployees = query.Count();
            var employees = query.OrderBy(e => e.Id)
                                           .Skip((pageNumber - 1) * 20)
                                           .Take(20)
                                           .ToList();

            ViewBag.TotalPages = (int)Math.Ceiling(totalEmployees / (double)20);
            ViewBag.CurrentPage = pageNumber;

            return View(employees);
        }

        [HttpGet("/Create")]
        public IActionResult Create()
        {
            var model = new Employee();
            return View(model);
        }

        [HttpPost("/Create")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Employee employee, IFormFile photo)
        {
            if (ModelState.IsValid)
            {
                if (photo != null && photo.Length > 0)
                {
                    using var ms = new MemoryStream();
                    photo.CopyTo(ms);
                    employee.Photo = ms.ToArray();
                }

                _empDbContext.Add(employee);
                _empDbContext.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        [HttpGet("/Edit/{id}")]
        public IActionResult Edit(int? id)
        {
            if (id == null) return NotFound();
            var employee = _empDbContext.employees.Find(id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost("/Edit/{id}")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Employee employee, IFormFile photo)
        {
            if (id != employee.Id) return NotFound();
            if (ModelState.IsValid)
            {
                if (photo != null && photo.Length > 0)
                {
                    using var ms = new MemoryStream();
                    photo.CopyTo(ms);
                    employee.Photo = ms.ToArray();
                }

                _empDbContext.Update(employee);
                _empDbContext.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        [HttpPost("/Delete")]
        public IActionResult Delete(int id)
        {
            var employee = _empDbContext.employees.Find(id);
            if (employee == null) return NotFound();

            _empDbContext.employees.Remove(employee);
            _empDbContext.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/BulkImport")]
        public IActionResult BulkImport()
        {
            return View();
        }

        [HttpPost("/BulkImport")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImport(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("File", "Please upload a valid file.");
                return View();
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (fileExtension != ".csv")
            {
                ModelState.AddModelError("File", "Only CSV files are allowed.");
                return View();
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var reader = new StreamReader(stream))
                    {
                        var employees = new List<Employee>();
                        int lineNumber = 0;
                        while (!reader.EndOfStream)
                        {
                            lineNumber++;
                            var line = await reader.ReadLineAsync();
                            var values = line.Split(',');

                            if (values.Length != 4)
                            {
                                ModelState.AddModelError("File", $"Invalid number of columns on line {lineNumber}");
                                continue;
                            }

                            if (!DateTime.TryParse(values[2], out DateTime dob))
                            {
                                ModelState.AddModelError("File", $"Invalid date format on line {lineNumber}: {values[2]}");
                                continue;
                            }

                            var employee = new Employee
                            {
                                Name = values[0],
                                Gender = values[1],
                                Dob = dob,
                                Place = values[3],
                                Photo = null
                            };

                            employees.Add(employee);
                        }

                        if (employees.Count == 0)
                        {
                            ModelState.AddModelError("File", "No valid employee data found in the file.");
                            return View();
                        }

                        try
                        {
                            await _empDbContext.employees.AddRangeAsync(employees);
                            await _empDbContext.SaveChangesAsync();
                        }
                        catch (Exception dbEx)
                        {
                            ModelState.AddModelError("Database", $"Error saving data to the database: {dbEx.Message}");
                            return View();
                        }
                    }
                }

                TempData["SuccessMessage"] = "Employees imported successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("File", $"An error occurred while processing the file: {ex.Message}");
                return View();
            }
        }

    }
}