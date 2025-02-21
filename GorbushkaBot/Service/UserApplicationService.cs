using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;

namespace GorbushkaBot.Service
{
    public class UserApplicationService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserApplicationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveUserApplicationAsync(Dictionary<string, string> userData, string folderUrl, long chatId, string telegramUsername = null)
        {
            var userApplication = new UserApplication
            {
                ChatId = chatId,
                FacePhoto = userData["face_photo"],
                Fio = userData["fio"],
                PhoneNumber = userData["phone_number"],
                PassportNumber = userData["passport_number"],
                Role = userData["role"],
                PassportIssueDate = userData["passport_issue_date"],
                PassportIssueDateEnd = userData.GetValueOrDefault("passport_issue_date_end",""),
                RegistrationAddress = userData.GetValueOrDefault("registration_address", ""),
                PassportPhotos = userData["passport_photo"],
                PropiskaPhoto = userData.GetValueOrDefault("propiska_photo",""),
                PavilionNumber = userData.GetValueOrDefault("pavilion_number", ""),
                RentalContract = userData.GetValueOrDefault("rental_contract", ""),
                PavilionPhotos = userData["pavilion_photo"],
                NomerSoiza = userData.GetValueOrDefault("soiuz_number", ""),
                FolderUrl = folderUrl,
                UserNameTg = telegramUsername
            };

            _dbContext.UserApplications.Add(userApplication);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> UserHasApplication(long chatId)
        {
            return await _dbContext.UserApplications.AnyAsync(u => u.ChatId == chatId);
        }
    }
}
