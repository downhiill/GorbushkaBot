using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Service
{
    public class ApplicationService
    {
        private readonly ApplicationDbContext _dbContext;

        public ApplicationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Метод для получения всех заявок
        public async Task<List<UserApplication>> GetAllApplicationsAsync()
        {
            return await _dbContext.UserApplications
                                   .OrderBy(a => a.CreatedAt)
                                   .ToListAsync();
        }

        // Метод для поиска заявки по ID
        public async Task<UserApplication> GetApplicationByIdAsync(long chatId)
        {
            return await _dbContext.UserApplications.FirstOrDefaultAsync(a => a.ChatId == chatId);
        }
        public async Task<(List<UserApplication> Applications, int TotalPages)> GetApplicationsByPageAsync(int page, int pageSize)
        {
            int totalCount = await _dbContext.UserApplications.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var applications = await _dbContext.UserApplications
                                               .OrderBy(a => a.CreatedAt)
                                               .Skip((page - 1) * pageSize)
                                               .Take(pageSize)
                                               .ToListAsync();

            return (applications, totalPages);
        }

        // Метод для удаления заявки
        public async Task<bool> DeleteApplicationAsync(long chatId)
        {
            var application = await _dbContext.UserApplications.FirstOrDefaultAsync(a => a.ChatId == chatId);
            if (application == null)
                return false;

            _dbContext.UserApplications.Remove(application);
            await _dbContext.SaveChangesAsync();
            return true;
        }



        // Форматирование одной заявки
        public string FormatApplication(UserApplication application)
        {
            return $"👤 ФИО: {application.Fio}\n" +
                   $"📄 Паспорт:{application.PassportNumber}, {application.PassportIssueDate}\n" +
                   $"📞 Телефон (контактный): {application.PhoneNumber}\n" +
                   $"🛠 Роль: {application.Role}\n" +
                   $"🏢 Павильон: {application.PavilionNumber}, {application.RentalContract}\n" +
                   $"🖼 Фото: \n Лицо: {application.FacePhoto}\n Паспорт: {application.PassportPhotos}\n Павильон: {application.PavilionPhotos}";
        }



        // Форматирование списка заявок
        public (string text, InlineKeyboardMarkup keyboard) FormatApplications(List<UserApplication> applications)
        {
            var formattedApplications = new StringBuilder();
            var inlineKeyboardButtons = new List<List<InlineKeyboardButton>>();

            foreach (var app in applications)
            {

                inlineKeyboardButtons.Add(new List<InlineKeyboardButton>
{
                    InlineKeyboardButton.WithCallbackData($"👤 {app.Fio} | 📞 {app.PhoneNumber} | 🛠 {app.Role}", $"application_{app.ChatId}")
                });
            }

            return (formattedApplications.ToString(), new InlineKeyboardMarkup(inlineKeyboardButtons));
        }


    }
}
