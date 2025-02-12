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
