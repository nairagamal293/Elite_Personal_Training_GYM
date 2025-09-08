using elite.DTOs;

namespace elite.Interfaces
{
    public interface ITrainerService
    {
        Task<IEnumerable<TrainerDto>> GetAllTrainersAsync();
        Task<TrainerDto> GetTrainerByIdAsync(int id);
        Task<TrainerDto> CreateTrainerAsync(TrainerCreateDto trainerCreateDto);
        Task<TrainerDto> UpdateTrainerAsync(int id, TrainerCreateDto trainerUpdateDto);
        Task<bool> DeleteTrainerAsync(int id);
        Task<IEnumerable<ClassDto>> GetTrainerClassesAsync(int trainerId);
        Task<IEnumerable<OnlineSessionDto>> GetTrainerSessionsAsync(int trainerId);
    }
}
