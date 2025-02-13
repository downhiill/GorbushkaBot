using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Service
{
    public class ApplicationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly TelegramBotClient _botClient;
        private readonly long _groupChatId = -1002341852869;

        public ApplicationService(ApplicationDbContext dbContext, TelegramBotClient botClient)
        {
            _dbContext = dbContext;
            _botClient = botClient;
        }

        // Метод для добавления пользователя в группу с настройкой прав
        public async Task<bool> AddUserToGroupWithRoleAsync(long applicantChatId)
        {
            var application = await GetApplicationByIdAsync(applicantChatId);
            if (application == null)
            {
                await _botClient.SendTextMessageAsync(applicantChatId, "Заявка не найдена.");
                return false;
            }

            // Генерация прав доступа в зависимости от роли пользователя
            ChatPermissions permissions;
            switch (application.Role)
            {
                case "Покупатель":
                    permissions = new ChatPermissions
                    {
                        CanSendMessages = true,
                        CanSendAudios = true,
                        CanSendDocuments = true,
                        CanSendPhotos = true,
                        CanSendVideos = true,
                        CanSendVideoNotes = true,
                        CanSendVoiceNotes = true,
                        CanSendOtherMessages = true
                    };
                    break;
                case "Продавец":
                    permissions = new ChatPermissions
                    {
                        CanSendMessages = false,
                        CanSendAudios = false,
                        CanSendDocuments = false,
                        CanSendPhotos = false,
                        CanSendVideos = false,
                        CanSendVideoNotes = false,
                        CanSendVoiceNotes = false,
                        CanSendOtherMessages = false
                    };
                    break;
                case "Покупатель,Продавец":
                    permissions = new ChatPermissions
                    {
                        CanSendMessages = true,
                        CanSendAudios = true,
                        CanSendDocuments = true,
                        CanSendPhotos = true,
                        CanSendVideos = true,
                        CanSendVideoNotes = true,
                        CanSendVoiceNotes = true,
                        CanSendOtherMessages = true
                    };
                    break;
                default:
                    await _botClient.SendTextMessageAsync(applicantChatId, "Роль пользователя не определена.");
                    return false;
            }

            try
            {
                // Приглашаем пользователя через ссылку
                var inviteLink = "https://t.me/+Nz5qqO88Qf4xNjRi"; // Замените на вашу ссылку для приглашения
                await _botClient.SendTextMessageAsync(applicantChatId, $"Для присоединения к группе, перейдите по следующей ссылке: {inviteLink}");

                // Ожидаем, пока пользователь присоединится (это необходимо для применения прав)
                // Здесь можно сделать ожидание или периодически проверять статус пользователя

                // Применение прав после присоединения пользователя в группу
                await _botClient.RestrictChatMemberAsync(_groupChatId, applicantChatId, permissions);

                // Уведомление о добавлении
                await _botClient.SendTextMessageAsync(_groupChatId, $"👤 {application.Fio} добавлен в чат с ролью: {application.Role}");

                return true;
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(applicantChatId, $"Ошибка при добавлении в чат: {ex.Message}");
                return false;
            }
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
