using elite.Data;
using elite.DTOs;
using elite.Interfaces;
using elite.Models;
using Microsoft.EntityFrameworkCore;

namespace elite.Services
{
    // Services/ClassService.cs
    public class ClassService : IClassService
    {
        private readonly GymDbContext _context;
        private readonly ILogger<ClassService> _logger;

        public ClassService(GymDbContext context, ILogger<ClassService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<ClassDto>> GetAllClassesAsync()
        {
            return await _context.Classes
                .Include(c => c.Trainer)
                .Select(c => new ClassDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Duration = c.Duration,
                    TrainerId = c.TrainerId,
                    TrainerName = c.Trainer.Name,
                    MaxCapacity = c.MaxCapacity,
                    Price = c.Price
                })
                .ToListAsync();
        }

        public async Task<ClassDto> GetClassByIdAsync(int id)
        {
            var classObj = await _context.Classes
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classObj == null) throw new ArgumentException("Class not found");

            return new ClassDto
            {
                Id = classObj.Id,
                Name = classObj.Name,
                Description = classObj.Description,
                Duration = classObj.Duration,
                TrainerId = classObj.TrainerId,
                TrainerName = classObj.Trainer.Name,
                MaxCapacity = classObj.MaxCapacity,
                Price = classObj.Price
            };
        }

        public async Task<ClassDto> CreateClassAsync(ClassCreateDto classCreateDto)
        {
            // Check if trainer exists
            var trainer = await _context.Trainers.FindAsync(classCreateDto.TrainerId);
            if (trainer == null) throw new ArgumentException("Trainer not found");

            var classObj = new Class
            {
                Name = classCreateDto.Name,
                Description = classCreateDto.Description,
                Duration = classCreateDto.Duration,
                TrainerId = classCreateDto.TrainerId,
                MaxCapacity = classCreateDto.MaxCapacity,
                Price = classCreateDto.Price
            };

            _context.Classes.Add(classObj);
            await _context.SaveChangesAsync();

            return new ClassDto
            {
                Id = classObj.Id,
                Name = classObj.Name,
                Description = classObj.Description,
                Duration = classObj.Duration,
                TrainerId = classObj.TrainerId,
                TrainerName = trainer.Name,
                MaxCapacity = classObj.MaxCapacity,
                Price = classObj.Price
            };
        }

        public async Task<ClassDto> UpdateClassAsync(int id, ClassCreateDto classUpdateDto)
        {
            var classObj = await _context.Classes.FindAsync(id);
            if (classObj == null) throw new ArgumentException("Class not found");

            // Check if trainer exists
            var trainer = await _context.Trainers.FindAsync(classUpdateDto.TrainerId);
            if (trainer == null) throw new ArgumentException("Trainer not found");

            classObj.Name = classUpdateDto.Name;
            classObj.Description = classUpdateDto.Description;
            classObj.Duration = classUpdateDto.Duration;
            classObj.TrainerId = classUpdateDto.TrainerId;
            classObj.MaxCapacity = classUpdateDto.MaxCapacity;
            classObj.Price = classUpdateDto.Price;

            _context.Classes.Update(classObj);
            await _context.SaveChangesAsync();

            return new ClassDto
            {
                Id = classObj.Id,
                Name = classObj.Name,
                Description = classObj.Description,
                Duration = classObj.Duration,
                TrainerId = classObj.TrainerId,
                TrainerName = trainer.Name,
                MaxCapacity = classObj.MaxCapacity,
                Price = classObj.Price
            };
        }

        public async Task<bool> DeleteClassAsync(int id)
        {
            var classObj = await _context.Classes.FindAsync(id);
            if (classObj == null) throw new ArgumentException("Class not found");

            // Check if class has any schedules
            var hasSchedules = await _context.ClassSchedules.AnyAsync(cs => cs.ClassId == id);
            if (hasSchedules) throw new InvalidOperationException("Cannot delete class with existing schedules");

            _context.Classes.Remove(classObj);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<ClassScheduleDto>> GetClassSchedulesAsync(int classId)
        {
            return await _context.ClassSchedules
                .Where(cs => cs.ClassId == classId)
                .Include(cs => cs.Class)
                .Select(cs => new ClassScheduleDto
                {
                    Id = cs.Id,
                    ClassId = cs.ClassId,
                    ClassName = cs.Class.Name,
                    StartTime = cs.StartTime,
                    EndTime = cs.EndTime,
                    Location = cs.Location,
                    AvailableSlots = cs.AvailableSlots,
                    MaxCapacity = cs.Class.MaxCapacity
                })
                .ToListAsync();
        }

        public async Task<ClassScheduleDto> CreateClassScheduleAsync(ClassScheduleCreateDto scheduleCreateDto)
        {
            var classObj = await _context.Classes.FindAsync(scheduleCreateDto.ClassId);
            if (classObj == null) throw new ArgumentException("Class not found");

            // Check if the schedule overlaps with existing schedules
            var overlappingSchedule = await _context.ClassSchedules
                .Where(cs => cs.ClassId == scheduleCreateDto.ClassId)
                .Where(cs => cs.StartTime < scheduleCreateDto.EndTime && cs.EndTime > scheduleCreateDto.StartTime)
                .FirstOrDefaultAsync();

            if (overlappingSchedule != null)
                throw new InvalidOperationException("Schedule overlaps with existing class schedule");

            var schedule = new ClassSchedule
            {
                ClassId = scheduleCreateDto.ClassId,
                StartTime = scheduleCreateDto.StartTime,
                EndTime = scheduleCreateDto.EndTime,
                Location = scheduleCreateDto.Location,
                AvailableSlots = classObj.MaxCapacity
            };

            _context.ClassSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            return new ClassScheduleDto
            {
                Id = schedule.Id,
                ClassId = schedule.ClassId,
                ClassName = classObj.Name,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                Location = schedule.Location,
                AvailableSlots = schedule.AvailableSlots,
                MaxCapacity = classObj.MaxCapacity
            };
        }

        public async Task<bool> DeleteClassScheduleAsync(int scheduleId)
        {
            var schedule = await _context.ClassSchedules.FindAsync(scheduleId);
            if (schedule == null) throw new ArgumentException("Class schedule not found");

            // Check if schedule has any bookings
            var hasBookings = await _context.Bookings.AnyAsync(b => b.ScheduleId == scheduleId && b.Type == "Class");
            if (hasBookings) throw new InvalidOperationException("Cannot delete schedule with existing bookings");

            _context.ClassSchedules.Remove(schedule);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
