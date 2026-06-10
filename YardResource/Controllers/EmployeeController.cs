using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YardResource.Data;

namespace YardResource.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _db;

        public EmployeeController(AppDbContext db)
        {
            _db = db;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst("UserId")!.Value);

        // Личный кабинет — главная
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            var user = await _db.Users
                .Include(u => u.Department)
                .Include(u => u.Position)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            ViewBag.TotalEnrollments = await _db.Enrollments
                .CountAsync(e => e.UserId == userId);
            ViewBag.CompletedCourses = await _db.Enrollments
                .CountAsync(e => e.UserId == userId && e.Status == "Завершён");
            ViewBag.ActiveCourses = await _db.Enrollments
                .CountAsync(e => e.UserId == userId && (e.Status == "Записан" || e.Status == "В процессе"));
            ViewBag.UnreadNotifications = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return View(user);
        }

        // Доступные курсы
        public async Task<IActionResult> Courses()
        {
            var userId = GetUserId();

            var courses = await _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .Include(c => c.Sessions)
                .Where(c => c.IsActive)
                .ToListAsync();

            var myEnrollments = await _db.Enrollments
                .Where(e => e.UserId == userId)
                .Select(e => e.CourseId)
                .ToListAsync();

            ViewBag.MyEnrollments = myEnrollments;
            return View(courses);
        }

        // Страница выбора сессии
        public async Task<IActionResult> SelectSession(int courseId)
        {
            var course = await _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Sessions)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();
            return View(course);
        }

        // Запись на курс с выбранной сессией
        [HttpPost]
        public async Task<IActionResult> Enroll(int courseId, int sessionId)
        {
            var userId = GetUserId();

            if (await _db.Enrollments.AnyAsync(e => e.UserId == userId
                && e.CourseId == courseId && e.Status != "Отменён"))
            {
                TempData["Error"] = "Вы уже записаны на этот курс.";
                return RedirectToAction("Courses");
            }

            var course = await _db.Courses
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

            if (course.Enrollments.Count(e => e.Status != "Отменён") >= course.MaxCapacity)
            {
                TempData["Error"] = "На этот курс нет свободных мест.";
                return RedirectToAction("Courses");
            }

            var newSession = await _db.CourseSessions.FindAsync(sessionId);
            if (newSession == null)
            {
                TempData["Error"] = "Выбранное занятие не найдено.";
                return RedirectToAction("SelectSession", new { courseId });
            }

            var mySessions = await _db.CourseSessions
                .Where(s => _db.Enrollments
                    .Where(e => e.UserId == userId && e.Status != "Отменён")
                    .Select(e => e.CourseId)
                    .Contains(s.CourseId))
                .ToListAsync();

            foreach (var ms in mySessions)
            {
                if (newSession.SessionDate == ms.SessionDate &&
                    newSession.StartTime < ms.EndTime &&
                    newSession.EndTime > ms.StartTime)
                {
                    TempData["Error"] = $"Конфликт расписания! У вас уже есть занятие {newSession.SessionDate:dd.MM.yyyy} в это время.";
                    return RedirectToAction("SelectSession", new { courseId });
                }
            }

            _db.Enrollments.Add(new Models.Enrollment
            {
                UserId = userId,
                CourseId = courseId,
                Status = "Записан",
                EnrolledAt = DateTime.Now
            });

            _db.Notifications.Add(new Models.Notification
            {
                UserId = userId,
                Title = "Успешная запись на курс",
                Message = $"Вы успешно записаны на курс \"{course.Title}\". Занятие: {newSession.SessionDate:dd.MM.yyyy} в {newSession.StartTime:hh\\:mm}.",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Вы успешно записаны на курс!";
            return RedirectToAction("MyCourses");
        }

        // Отписаться от курса
        [HttpPost]
        public async Task<IActionResult> Unenroll(int courseId)
        {
            var userId = GetUserId();
            var enrollment = await _db.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.UserId == userId
                    && e.CourseId == courseId
                    && e.Status == "Записан");

            if (enrollment == null)
            {
                TempData["Error"] = "Вы не можете отписаться от этого курса.";
                return RedirectToAction("MyCourses");
            }

            enrollment.Status = "Отменён";

            _db.Notifications.Add(new Models.Notification
            {
                UserId = userId,
                Title = "Отмена записи на курс",
                Message = $"Вы отменили запись на курс \"{enrollment.Course?.Title}\".",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Вы успешно отписались от курса.";
            return RedirectToAction("MyCourses");
        }

        // Мои курсы
        public async Task<IActionResult> MyCourses()
        {
            var userId = GetUserId();
            var enrollments = await _db.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c!.Category)
                .Include(e => e.Course)
                    .ThenInclude(c => c!.Instructor)
                .Include(e => e.Result)
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            return View(enrollments);
        }

        // Расписание
        public async Task<IActionResult> Schedule()
        {
            var userId = GetUserId();
            var sessions = await _db.CourseSessions
                .Include(s => s.Course)
                .Where(s => _db.Enrollments
                    .Where(e => e.UserId == userId && e.Status != "Отменён")
                    .Select(e => e.CourseId)
                    .Contains(s.CourseId))
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            return View(sessions);
        }

        // Уведомления
        public async Task<IActionResult> Notifications()
        {
            var userId = GetUserId();
            var notifications = await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var unread = notifications.Where(n => !n.IsRead).ToList();
            unread.ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();

            return View(notifications);
        }

        // Профиль
        public async Task<IActionResult> Profile()
        {
            var userId = GetUserId();
            var user = await _db.Users
                .Include(u => u.Department)
                .Include(u => u.Position)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            return View(user);
        }
    }
}