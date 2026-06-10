using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YardResource.Data;
using YardResource.Models;

namespace YardResource.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db)
        {
            _db = db;
        }

        // Дашборд
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _db.Users.CountAsync(u => u.IsActive);
            ViewBag.TotalCourses = await _db.Courses.CountAsync(c => c.IsActive);
            ViewBag.TotalEnrollments = await _db.Enrollments.CountAsync();
            ViewBag.ActiveEnrollments = await _db.Enrollments
                .CountAsync(e => e.Status == "Записан" || e.Status == "В процессе");
            return View();
        }

        // ===== СОТРУДНИКИ =====
        public async Task<IActionResult> Users()
        {
            var users = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .OrderBy(u => u.LastName)
                .ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> UserDetails(int id)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .Include(u => u.Enrollments)
                    .ThenInclude(e => e.Course)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null) return NotFound();
            return View(user);
        }

        public async Task<IActionResult> CreateUser()
        {
            ViewBag.Roles = await _db.Roles.ToListAsync();
            ViewBag.Departments = await _db.Departments.ToListAsync();
            ViewBag.Positions = await _db.Positions.ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(User user, string password)
        {
            user.PasswordHash = AccountController.HashPassword(password);
            user.CreatedAt = DateTime.Now;
            user.HireDate = DateTime.Now;
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return RedirectToAction("Users");
        }

        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            ViewBag.Roles = await _db.Roles.ToListAsync();
            ViewBag.Departments = await _db.Departments.ToListAsync();
            ViewBag.Positions = await _db.Positions.ToListAsync();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(User user)
        {
            var existing = await _db.Users.FindAsync(user.UserId);
            if (existing == null) return NotFound();

            existing.LastName = user.LastName;
            existing.FirstName = user.FirstName;
            existing.MiddleName = user.MiddleName;
            existing.Email = user.Email;
            existing.Phone = user.Phone;
            existing.RoleId = user.RoleId;
            existing.DepartmentId = user.DepartmentId;
            existing.PositionId = user.PositionId;
            existing.IsActive = user.IsActive;

            await _db.SaveChangesAsync();
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = false;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Users");
        }

        // ===== КУРСЫ =====
        public async Task<IActionResult> Courses()
        {
            var courses = await _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(courses);
        }

        public async Task<IActionResult> CreateCourse()
        {
            ViewBag.Categories = await _db.CourseCategories.ToListAsync();
            ViewBag.Instructors = await _db.Users
                .Where(u => u.RoleId == 3)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateCourse(Course course)
        {
            var adminId = int.Parse(User.FindFirst("UserId")!.Value);
            course.CreatedBy = adminId;
            course.CreatedAt = DateTime.Now;
            _db.Courses.Add(course);
            await _db.SaveChangesAsync();
            return RedirectToAction("Courses");
        }

        public async Task<IActionResult> EditCourse(int id)
        {
            var course = await _db.Courses.FindAsync(id);
            if (course == null) return NotFound();
            ViewBag.Categories = await _db.CourseCategories.ToListAsync();
            ViewBag.Instructors = await _db.Users
                .Where(u => u.RoleId == 3)
                .ToListAsync();
            return View(course);
        }

        [HttpPost]
        public async Task<IActionResult> EditCourse(Course course)
        {
            var existing = await _db.Courses.FindAsync(course.CourseId);
            if (existing == null) return NotFound();

            existing.Title = course.Title;
            existing.Description = course.Description;
            existing.CategoryId = course.CategoryId;
            existing.InstructorId = course.InstructorId;
            existing.MaxCapacity = course.MaxCapacity;
            existing.DurationHours = course.DurationHours;
            existing.IsActive = course.IsActive;

            await _db.SaveChangesAsync();
            return RedirectToAction("Courses");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _db.Courses
                .Include(c => c.Sessions)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Result)
                .Include(c => c.TargetPositions)
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course != null)
            {
                // Удаляем результаты
                foreach (var e in course.Enrollments)
                {
                    if (e.Result != null)
                        _db.CourseResults.Remove(e.Result);
                }

                // Удаляем записи
                _db.Enrollments.RemoveRange(course.Enrollments);

                // Удаляем сессии
                _db.CourseSessions.RemoveRange(course.Sessions);

                // Удаляем целевые должности
                _db.CourseTargetPositions.RemoveRange(course.TargetPositions);

                // Удаляем сам курс
                _db.Courses.Remove(course);

                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Courses");
        }

        // ===== СЕССИИ КУРСА =====
        public async Task<IActionResult> CourseSessions(int id)
        {
            var course = await _db.Courses
                .Include(c => c.Sessions)
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        public async Task<IActionResult> AddSession(int courseId, DateTime sessionDate,
            TimeSpan startTime, TimeSpan endTime, string? location)
        {
            var course = await _db.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            // Проверка конфликта расписания у преподавателя
            var conflict = await _db.CourseSessions
                .Include(s => s.Course)
                .Where(s => s.Course!.InstructorId == course.InstructorId
                         && s.SessionDate == sessionDate
                         && s.CourseId != courseId
                         && s.StartTime < endTime
                         && s.EndTime > startTime)
                .FirstOrDefaultAsync();

            if (conflict != null)
            {
                TempData["Error"] = $"Конфликт! Преподаватель уже ведёт курс \"{conflict.Course?.Title}\" " +
                                    $"{conflict.SessionDate:dd.MM.yyyy} с {conflict.StartTime:hh\\:mm} до {conflict.EndTime:hh\\:mm}.";
                return RedirectToAction("CourseSessions", new { id = courseId });
            }

            _db.CourseSessions.Add(new CourseSession
            {
                CourseId = courseId,
                SessionDate = sessionDate,
                StartTime = startTime,
                EndTime = endTime,
                Location = location
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Занятие успешно добавлено!";
            return RedirectToAction("CourseSessions", new { id = courseId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSession(int sessionId, int courseId)
        {
            var session = await _db.CourseSessions.FindAsync(sessionId);
            if (session != null)
            {
                _db.CourseSessions.Remove(session);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("CourseSessions", new { id = courseId });
        }

        // ===== ЗАПИСИ НА КУРСЫ =====
        public async Task<IActionResult> Enrollments()
        {
            var enrollments = await _db.Enrollments
                .Include(e => e.User)
                .Include(e => e.Course)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();
            return View(enrollments);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEnrollmentStatus(int id, string status)
        {
            var enrollment = await _db.Enrollments.FindAsync(id);
            if (enrollment != null)
            {
                enrollment.Status = status;
                if (status == "Завершён")
                {
                    enrollment.CompletedAt = DateTime.Now;
                    enrollment.CertificateNo = $"YARD-{DateTime.Now.Year}-{id:D4}";
                }
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Enrollments");
        }
    }
}