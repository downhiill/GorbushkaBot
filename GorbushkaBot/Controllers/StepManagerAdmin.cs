using GorbushkaBot.Service;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class StepManagerAdmin
    {
        private static readonly Dictionary<long, (string step, int messageId)> AdminSteps = new();
        private readonly ApplicationService _applicationService;

        public StepManagerAdmin(ApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public void SaveStep(long chatId, string step, int messageId)
        {
            AdminSteps[chatId] = (step, messageId);
        }

        public async Task HandleMessage(ITelegramBotClient botClient, long chatId, Message message)
        {
            if (!AdminSteps.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Выберите действие из меню.");
                return;
            }

            var (step, messageId) = AdminSteps[chatId];

            if (step == "waiting_for_application_id")
            {
                int applicationId;
                if (!int.TryParse(message.Text, out applicationId))
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: Введите корректный ID заявки (число).");
                    return;
                }

                // Ищем заявку по ID
                var application = await _applicationService.GetApplicationByIdAsync(applicationId);

                if (application == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Заявка не найдена.");
                }
                else
                {
                    string formattedApplication = _applicationService.FormatApplication(application);
                    await botClient.SendTextMessageAsync(chatId, formattedApplication);
                }

                // После обработки заявки возвращаем в главное меню
                var menuKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Найти заявку", "find_application") },
                    new[] { InlineKeyboardButton.WithCallbackData("Заявки", "applications") }
                });

                await botClient.SendTextMessageAsync(chatId, "Меню администратора", replyMarkup: menuKeyboard);

                // Очищаем шаг
                AdminSteps.Remove(chatId);
            }
        }


        public async Task HandleCallbackQuery(ITelegramBotClient botClient, long chatId, CallbackQuery callbackQuery)
        {
            if (callbackQuery?.Message == null)
            {
                await botClient.SendTextMessageAsync(chatId, "Произошла ошибка: callbackQuery.Message == null");
                return;
            }

            string data = callbackQuery.Data;

            if (data == "find_application")
            {
                // Отправляем запрос на ввод ID заявки
                await botClient.SendTextMessageAsync(chatId, "Введите ID заявки для поиска:");

                // Сохраняем шаг ожидания ID заявки
                SaveStep(chatId, "waiting_for_application_id", callbackQuery.Message.MessageId);
            }
            else if (data == "applications")
            {
                var applications = await _applicationService.GetAllApplicationsAsync();
                if (applications == null || applications.Count == 0)
                {
                    await botClient.SendTextMessageAsync(chatId, "Заявки не найдены.");
                }
                else
                {
                    string formattedApplications = _applicationService.FormatApplications(applications);
                    await botClient.SendTextMessageAsync(chatId, formattedApplications);
                }
            }
        }

    }
}
