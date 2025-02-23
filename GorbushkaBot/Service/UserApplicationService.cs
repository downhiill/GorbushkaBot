using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GorbushkaBot.Service
{
    public class UserApplicationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly long _groupChatId = -1002341852869;
        private readonly TelegramBotClient _botClient;

        public UserApplicationService(ApplicationDbContext dbContext, TelegramBotClient botClient)
        {
            _dbContext = dbContext;
            _botClient = botClient;
        }

        public async Task SaveUserApplicationAsync(Dictionary<string, string> userData, string folderUrl, long chatId, string telegramUsername)
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

        public async Task<bool> AddUserToBlacklistAsync(long chatId)
        {
            try
            {
                Console.WriteLine($"Попытка добавить пользователя {chatId} в черный список.");

                // Проверяем, что _dbContext инициализирован
                if (_dbContext == null)
                {
                    Console.WriteLine("Ошибка: _dbContext не инициализирован.");
                    return false;
                }

                // Проверяем, что _botClient инициализирован
                if (_botClient == null)
                {
                    Console.WriteLine("Ошибка: _botClient не инициализирован.");
                    return false;
                }

                // Проверяем, что _groupChatId задан
                if (_groupChatId == 0)
                {
                    Console.WriteLine("Ошибка: _groupChatId не задан.");
                    return false;
                }

                // Проверяем, есть ли пользователь уже в черном списке
                var existingBlacklistEntry = await _dbContext.BlacklistEntries
                    .FirstOrDefaultAsync(b => b.ChatId == chatId);

                if (existingBlacklistEntry != null)
                {
                    Console.WriteLine($"Пользователь {chatId} уже в черном списке.");
                    return false; // Пользователь уже в черном списке
                }

                // Добавляем пользователя в черный список
                _dbContext.BlacklistEntries.Add(new BlacklistEntry
                {
                    ChatId = chatId,
                    CreatedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Пользователь {chatId} добавлен в черный список.");

                // Блокируем пользователя в чате
                await _botClient.BanChatMemberAsync(
                    _groupChatId,
                    chatId
                );

                Console.WriteLine($"Пользователь {chatId} заблокирован в группе.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении пользователя в черный список: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveUserFromBlacklistAsync(long chatId)
        {
            try
            {
                Console.WriteLine($"Попытка удалить пользователя {chatId} из черного списка.");

                var blacklistEntry = await _dbContext.BlacklistEntries
                    .FirstOrDefaultAsync(b => b.ChatId == chatId);

                if (blacklistEntry == null)
                {
                    Console.WriteLine($"Пользователь {chatId} не найден в черном списке.");
                    return false;
                }

                _dbContext.BlacklistEntries.Remove(blacklistEntry);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Пользователь {chatId} удален из черного списка.");

                // Разблокируем пользователя в чате
                await _botClient.UnbanChatMemberAsync(
                    _groupChatId,
                    chatId
                );

                Console.WriteLine($"Пользователь {chatId} разблокирован в группе.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении пользователя из черного списка: {ex.Message}");
                return false;
            }
        }
    }
}
