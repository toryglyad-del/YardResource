using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YardResource.Data;

namespace YardResource.Controllers
{
    [Authorize(Roles = "Instructor")]
    public class InstructorController : Controller
    {
        private readonly AppDbContext _db;

        public InstructorController(AppDbContext db)
        {
            _db = db;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst("UserId")!.Value);

        // Главная
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();

            ViewBag.TotalCourses = await _db.Courses
                .CountAsync(c => c.InstructorId == userId && c.IsActive);
            ViewBag.TotalStudents = await _db.Enrollments
                .CountAsync(e => e.Course!.InstructorId == userId && e.Status != "Отменён");
            ViewBag.CompletedStudents = await _db.Enrollments
                .CountAsync(e => e.Course!.InstructorId == userId && e.Status == "Завершён");

            var courses = await _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Enrollments)
                .Where(c => c.InstructorId == userId && c.IsActive)
                .ToListAsync();

            return View(courses);
        }

        // Студенты курса
        public async Task<IActionResult> Students(int courseId)
        {
            var userId = GetUserId();

            var course = await _db.Courses
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.User)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Result)
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.InstructorId == userId);

            if (course == null) return NotFound();
            return View(course);
        }

        // Выставить оценку
        [HttpPost]
        public async Task<IActionResult> GradeStudent(int enrollmentId, decimal score, string? feedback)
        {
            var enrollment = await _db.Enrollments
                .Include(e => e.Result)
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

            if (enrollment == null) return NotFound();

            if (enrollment.Result == null)
            {
                _db.CourseResults.Add(new Models.CourseResult
                {
                    EnrollmentId = enrollmentId,
                    Score = score,
                    IsPassed = score >= 3,
                    Feedback = feedback,
                    GradedAt = DateTime.Now,
                    GradedBy = GetUserId()
                });
            }
            else
            {
                enrollment.Result.Score = score;
                enrollment.Result.IsPassed = score >= 3;
                enrollment.Result.Feedback = feedback;
                enrollment.Result.GradedAt = DateTime.Now;
            }

            enrollment.Status = "Завершён";
            enrollment.CompletedAt = DateTime.Now;
            enrollment.CertificateNo = $"YARD-{DateTime.Now.Year}-{enrollmentId:D4}";

            _db.Notifications.Add(new Models.Notification
            {
                UserId = enrollment.UserId,
                Title = "Результат курса",
                Message = $"По курсу \"{enrollment.Course?.Title}\" выставлена оценка: {score}. " +
                          (score >= 3 ? "Поздравляем, курс пройден!" : "К сожалению, курс не пройден."),
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            return RedirectToAction("Students", new { courseId = enrollment.CourseId });
        }
    }
}