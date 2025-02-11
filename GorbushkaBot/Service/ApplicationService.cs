using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;

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

        // Форматирование одной заявки
        public string FormatApplication(UserApplication application)
        {
            return $"👤 ФИО: {application.Fio}\n" +
                   $"📞 Телефон: {application.PhoneNumber}\n" +
                   $"🛠 Роль: {application.Role}\n" +
                   $"📅 Дата заявки: {application.CreatedAt:dd.MM.yyyy}";
        }

        // Форматирование списка заявок
        public string FormatApplications(List<UserApplication> applications)
        {
            var formattedApplications = new StringBuilder();
            foreach (var app in applications)
            {
                formattedApplications.AppendLine($"ФИО: {app.Fio}, Телефон: {app.PhoneNumber}, Роль: {app.Role}");
                formattedApplications.AppendLine("-------------------");
            }
            return formattedApplications.ToString();
        }
    }
}
