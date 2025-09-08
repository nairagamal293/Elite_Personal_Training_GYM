using elite.Data;
using elite.DTOs;
using elite.Interfaces;
using elite.Models;
using Microsoft.EntityFrameworkCore;

namespace elite.Services
{
    public class BookingService : IBookingService
    {
        private readonly GymDbContext _context;
        private readonly ILogger<BookingService> _logger;

        public BookingService(GymDbContext context, ILogger<BookingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BookingResponseDto> CreateBookingAsync(CreateBookingDto bookingDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var user = await _context.Users
                    .Include(u => u.Membership)
                    .FirstOrDefaultAsync(u => u.Id == bookingDto.UserId);

                if (user == null) throw new ArgumentException("User not found");

                if (user.Membership == null || user.Membership.Status != "Active")
                    throw new InvalidOperationException("User does not have an active membership");

                if (user.Membership.EndDate < DateTime.UtcNow.Date)
                {
                    user.Membership.Status = "Expired";
                    await _context.SaveChangesAsync();
                    throw new InvalidOperationException("Membership has expired");
                }

                decimal duration = 0;
                string className = string.Empty;
                string trainerName = string.Empty;
                DateTime startTime, endTime;
                string location = string.Empty;

                if (bookingDto.Type == "Class")
                {
                    var schedule = await _context.ClassSchedules
                        .Include(cs => cs.Class)
                        .ThenInclude(c => c.Trainer)
                        .FirstOrDefaultAsync(cs => cs.Id == bookingDto.ScheduleId);

                    if (schedule == null) throw new ArgumentException("Class schedule not found");
                    if (schedule.AvailableSlots <= 0) throw new InvalidOperationException("No available slots");
                    if (schedule.StartTime <= DateTime.UtcNow) throw new InvalidOperationException("Class has already started");

                    // FIX: Convert minutes to hours
                    duration = schedule.Class.Duration / 60.0m;
                    className = schedule.Class.Name;
                    trainerName = schedule.Class.Trainer.Name;
                    startTime = schedule.StartTime;
                    endTime = schedule.EndTime;
                    location = schedule.Location;

                    var existingBooking = await _context.Bookings
                        .FirstOrDefaultAsync(b => b.UserId == bookingDto.UserId &&
                                                 b.Type == "Class" &&
                                                 b.ScheduleId == bookingDto.ScheduleId &&
                                                 b.Status == "Confirmed");

                    if (existingBooking != null) throw new InvalidOperationException("User already booked this class");

                    schedule.AvailableSlots--;
                    _context.ClassSchedules.Update(schedule);
                }
                else if (bookingDto.Type == "OnlineSession")
                {
                    var schedule = await _context.OnlineSessionSchedules
                        .Include(oss => oss.OnlineSession)
                        .ThenInclude(os => os.Trainer)
                        .FirstOrDefaultAsync(oss => oss.Id == bookingDto.ScheduleId);

                    if (schedule == null) throw new ArgumentException("Online session schedule not found");
                    if (schedule.AvailableSlots <= 0) throw new InvalidOperationException("No available slots");
                    if (schedule.StartTime <= DateTime.UtcNow) throw new InvalidOperationException("Session has already started");

                    // FIX: Convert minutes to hours
                    duration = schedule.OnlineSession.Duration / 60.0m;
                    className = schedule.OnlineSession.Name;
                    trainerName = schedule.OnlineSession.Trainer.Name;
                    startTime = schedule.StartTime;
                    endTime = schedule.EndTime;

                    schedule.AvailableSlots--;
                    _context.OnlineSessionSchedules.Update(schedule);
                }
                else
                {
                    throw new ArgumentException("Invalid booking type");
                }

                if (user.Membership.RemainingHours < duration)
                    throw new InvalidOperationException("Insufficient hours in membership");

                user.Membership.RemainingHours -= duration;
                _context.Memberships.Update(user.Membership);

                var booking = new Booking
                {
                    UserId = bookingDto.UserId,
                    Type = bookingDto.Type,
                    ScheduleId = bookingDto.ScheduleId,
                    BookingDate = DateTime.UtcNow,
                    Status = "Confirmed",
                    HoursConsumed = duration
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new BookingResponseDto
                {
                    Id = booking.Id,
                    Type = booking.Type,
                    ScheduleId = booking.ScheduleId,
                    BookingDate = booking.BookingDate,
                    Status = booking.Status,
                    HoursConsumed = booking.HoursConsumed,
                    ClassName = className,
                    TrainerName = trainerName,
                    StartTime = startTime,
                    EndTime = endTime,
                    Location = location
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating booking");
                throw;
            }
        }

        public async Task<bool> CancelBookingAsync(int bookingId, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .ThenInclude(u => u.Membership)
                    .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

                if (booking == null) throw new ArgumentException("Booking not found");
                if (booking.Status != "Confirmed") throw new InvalidOperationException("Booking cannot be cancelled");

                DateTime scheduleTime;
                decimal duration = booking.HoursConsumed;

                if (booking.Type == "Class")
                {
                    var schedule = await _context.ClassSchedules.FindAsync(booking.ScheduleId);
                    if (schedule == null) throw new ArgumentException("Class schedule not found");

                    scheduleTime = schedule.StartTime;

                    // Check cancellation policy (>24 hours)
                    if ((scheduleTime - DateTime.UtcNow).TotalHours > 24)
                    {
                        // Refund hours
                        booking.User.Membership.RemainingHours += duration; // FIX: Removed (int) cast
                        _context.Memberships.Update(booking.User.Membership);

                        // Increment available slots
                        schedule.AvailableSlots++;
                        _context.ClassSchedules.Update(schedule);
                    }
                }
                else
                {
                    var schedule = await _context.OnlineSessionSchedules.FindAsync(booking.ScheduleId);
                    if (schedule == null) throw new ArgumentException("Online session schedule not found");

                    scheduleTime = schedule.StartTime;

                    // Check cancellation policy (>24 hours)
                    if ((scheduleTime - DateTime.UtcNow).TotalHours > 24)
                    {
                        // Refund hours
                        booking.User.Membership.RemainingHours += duration; // FIX: Removed (int) cast
                        _context.Memberships.Update(booking.User.Membership);

                        // Increment available slots
                        schedule.AvailableSlots++;
                        _context.OnlineSessionSchedules.Update(schedule);
                    }
                }

                booking.Status = "Cancelled";
                _context.Bookings.Update(booking);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling booking");
                throw;
            }
        }

        public async Task<IEnumerable<BookingResponseDto>> GetUserBookingsAsync(int userId)
        {
            var bookings = await _context.Bookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .Select(b => new BookingResponseDto
                {
                    Id = b.Id,
                    Type = b.Type,
                    ScheduleId = b.ScheduleId,
                    BookingDate = b.BookingDate,
                    Status = b.Status,
                    HoursConsumed = b.HoursConsumed,
                    ClassName = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Class.Name)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.OnlineSession.Name)
                            .FirstOrDefault(),
                    TrainerName = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Class.Trainer.Name)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.OnlineSession.Trainer.Name)
                            .FirstOrDefault(),
                    StartTime = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.StartTime)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.StartTime)
                            .FirstOrDefault(),
                    EndTime = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.EndTime)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.EndTime)
                            .FirstOrDefault(),
                    Location = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Location)
                            .FirstOrDefault()
                        : "Online"
                })
                .ToListAsync();

            return bookings;
        }

        // Services/BookingService.cs
        public async Task<BookingResponseDto> GetBookingDetailsAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.User) // Include the User navigation property
                .Where(b => b.Id == bookingId)
                .Select(b => new BookingResponseDto
                {
                    Id = b.Id,
                    Type = b.Type,
                    ScheduleId = b.ScheduleId,
                    BookingDate = b.BookingDate,
                    Status = b.Status,
                    HoursConsumed = b.HoursConsumed,
                    UserId = b.UserId,
                    UserName = b.User.Name,
                    ClassName = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Class.Name)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.OnlineSession.Name)
                            .FirstOrDefault(),
                    TrainerName = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Class.Trainer.Name)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.OnlineSession.Trainer.Name)
                            .FirstOrDefault(),
                    StartTime = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.StartTime)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.StartTime)
                            .FirstOrDefault(),
                    EndTime = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.EndTime)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.EndTime)
                            .FirstOrDefault(),
                    Location = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Location)
                            .FirstOrDefault()
                        : "Online"
                })
                .FirstOrDefaultAsync();

            if (booking == null) throw new ArgumentException("Booking not found");
            return booking;
        }




        public async Task<IEnumerable<BookingResponseDto>> GetBookingsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var bookings = await _context.Bookings
                .Where(b => b.BookingDate >= startDate && b.BookingDate <= endDate)
                .OrderByDescending(b => b.BookingDate)
                .Select(b => new BookingResponseDto
                {
                    Id = b.Id,
                    Type = b.Type,
                    ScheduleId = b.ScheduleId,
                    BookingDate = b.BookingDate,
                    Status = b.Status,
                    HoursConsumed = b.HoursConsumed,
                    ClassName = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Class.Name)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.OnlineSession.Name)
                            .FirstOrDefault(),
                    TrainerName = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Class.Trainer.Name)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.OnlineSession.Trainer.Name)
                            .FirstOrDefault(),
                    StartTime = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.StartTime)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.StartTime)
                            .FirstOrDefault(),
                    EndTime = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.EndTime)
                            .FirstOrDefault()
                        : _context.OnlineSessionSchedules
                            .Where(oss => oss.Id == b.ScheduleId)
                            .Select(oss => oss.EndTime)
                            .FirstOrDefault(),
                    Location = b.Type == "Class"
                        ? _context.ClassSchedules
                            .Where(cs => cs.Id == b.ScheduleId)
                            .Select(cs => cs.Location)
                            .FirstOrDefault()
                        : "Online"
                })
                .ToListAsync();

            return bookings;
        }
    }
}