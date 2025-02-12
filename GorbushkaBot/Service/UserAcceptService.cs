using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;

namespace GorbushkaBot.Service
{
    public class UserAcceptService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserAcceptService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveUserAcceptAsync(Dictionary<string, string> userData, string folderUrl, long chatId)
        {
            var userAccept = new UserAccept
            {
                ChatId = chatId,
                FacePhoto = userData["face_photo"],
                Fio = userData["fio"],
                PhoneNumber = userData["phone_number"],
                PassportNumber = userData["passport_number"],
                Role = userData["role"],
                PassportIssueDate = userData["passport_issue_date"],
                RegistrationAddress = userData.GetValueOrDefault("registration_address"),
                PassportPhotos = userData["passport_photo"],
                PavilionNumber = userData.GetValueOrDefault("pavilion_number", ""),
                RentalContract = userData.GetValueOrDefault("rental_contract", ""),
                PavilionPhotos = userData["pavilion_photo"],
                FolderUrl = folderUrl
            };

            _dbContext.UserAccepts.Add(userAccept);
            await _dbContext.SaveChangesAsync();
        }
    }
}
