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

        public async Task SaveUserApplicationAsync(Dictionary<string, string> userData, string folderUrl)
        {
            var userApplication = new UserApplication
            {
                FacePhoto = userData["face_photo"],
                Fio = userData["fio"],
                PhoneNumber = userData["phone_number"],
                PassportNumber = userData["passport_number"],
                PassportIssueDate = userData["passport_issue_date"],
                RegistrationAddress = userData["registration_address"],
                PassportPhotos = userData["passport_photos"],
                PavilionNumber = userData.GetValueOrDefault("pavilion_number", ""),
                RentalContract = userData.GetValueOrDefault("rental_contract", ""),
                PavilionPhotos = userData["pavilion_photos"],
                FolderUrl = folderUrl
            };

            _dbContext.UserApplications.Add(userApplication);
            await _dbContext.SaveChangesAsync();
        }
    }
}
