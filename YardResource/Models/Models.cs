using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YardResource.Models
{
    public class Role
    {
        [Key] public int RoleId { get; set; }
        [Required] public string Name { get; set; } = "";
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    public class Department
    {
        [Key] public int DepartmentId { get; set; }
        [Required] public string Name { get; set; } = "";
        public string? Description { get; set; }
        public ICollection<Position> Positions { get; set; } = new List<Position>();
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    public class Position
    {
        [Key] public int PositionId { get; set; }
        [Required] public string Name { get; set; } = "";
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<CourseTargetPosition> CourseTargetPositions { get; set; } = new List<CourseTargetPosition>();
    }

    public class User
    {
        [Key] public int UserId { get; set; }
        [Required] public string LastName { get; set; } = "";
        [Required] public string FirstName { get; set; } = "";
        public string? MiddleName { get; set; }
        [Required] public string Email { get; set; } = "";
        [Required] public string PasswordHash { get; set; } = "";
        public string? Phone { get; set; }
        public string? PhotoPath { get; set; }
        public DateTime HireDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public int RoleId { get; set; }
        public Role? Role { get; set; }
        public int? PositionId { get; set; }
        public Position? Position { get; set; }
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
    }

    public class CourseCategory
    {
        [Key] public int CategoryId { get; set; }
        [Required] public string Name { get; set; } = "";
        public string? Description { get; set; }
        public ICollection<Course> Courses { get; set; } = new List<Course>();
    }

    public class Course
    {
        [Key] public int CourseId { get; set; }
        [Required] public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public CourseCategory? Category { get; set; }
        public int InstructorId { get; set; }
        public User? Instructor { get; set; }
        public int MaxCapacity { get; set; } = 20;
        public int DurationHours { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CreatedBy { get; set; }
        public ICollection<CourseSession> Sessions { get; set; } = new List<CourseSession>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<CourseTargetPosition> TargetPositions { get; set; } = new List<CourseTargetPosition>();
    }

    public class CourseTargetPosition
    {
        [Key] public int Id { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        public int PositionId { get; set; }
        public Position? Position { get; set; }
    }

    public class CourseSession
    {
        [Key] public int SessionId { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Location { get; set; }
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }

    public class Enrollment
    {
        [Key] public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Записан";
        public DateTime? CompletedAt { get; set; }
        public string? CertificateNo { get; set; }
        public CourseResult? Result { get; set; }
    }

    public class Attendance
    {
        [Key] public int AttendanceId { get; set; }
        public int EnrollmentId { get; set; }
        public Enrollment? Enrollment { get; set; }
        public int SessionId { get; set; }
        public CourseSession? Session { get; set; }
        public bool IsPresent { get; set; } = false;
        public string? Note { get; set; }
    }

    public class CourseResult
    {
        [Key] public int ResultId { get; set; }
        public int EnrollmentId { get; set; }
        public Enrollment? Enrollment { get; set; }
        public decimal? Score { get; set; }
        public bool IsPassed { get; set; } = false;
        public string? Feedback { get; set; }
        public DateTime? GradedAt { get; set; }
        public int? GradedBy { get; set; }
    }

    public class Notification
    {
        [Key] public int NotificationId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        [Required] public string Title { get; set; } = "";
        [Required] public string Message { get; set; } = "";
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}