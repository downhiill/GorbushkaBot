using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;

namespace GorbushkaBot.Service
{
    public class UserApplicationService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserApplicationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveUserApplicationAsync(Dictionary<string, string> userData, string folderUrl, long chatId)
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
                PropiskaPhotos = userData.GetValueOrDefault("propiska_photo",""),
                PavilionNumber = userData.GetValueOrDefault("pavilion_number", ""),
                RentalContract = userData.GetValueOrDefault("rental_contract", ""),
                PavilionPhotos = userData["pavilion_photo"],
                FolderUrl = folderUrl
            };

            _dbContext.UserApplications.Add(userApplication);
            await _dbContext.SaveChangesAsync();
        }
    }
}
