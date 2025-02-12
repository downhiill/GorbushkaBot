using GorbushkaBot.Service;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class StepManagerAdmin
    {
        private static readonly Dictionary<long, (string step, int messageId)> AdminSteps = new();
        private static readonly Dictionary<long, int> AdminCurrentPage = new();
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
                if (!int.TryParse(message.Text, out int applicationId))
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

                    // Создаем кнопки для одобрения и отклонения заявки
                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"approve_{application.ChatId}"),
                            InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"reject_{application.ChatId}")
                        }
                    });

                    await botClient.SendTextMessageAsync(chatId, formattedApplication, replyMarkup: inlineKeyboard);
                }

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
                await botClient.SendTextMessageAsync(chatId, "Введите ID заявки для поиска:");
                SaveStep(chatId, "waiting_for_application_id", callbackQuery.Message.MessageId);
            }
            else if (data.StartsWith("applications"))
            {
                int page = 0;
                var parts = data.Split('_');
                if (parts.Length > 1 && int.TryParse(parts[1], out int parsedPage))
                {
                    page = parsedPage;
                }

                const int pageSize = 10;
                var applications = await _applicationService.GetAllApplicationsAsync();

                if (applications == null || applications.Count == 0)
                {
                    await botClient.SendTextMessageAsync(chatId, "Заявки не найдены.");
                    return;
                }

                var paginatedApplications = applications.Skip(page * pageSize).Take(pageSize).ToList();

                if (paginatedApplications.Count == 0)
                {
                    await botClient.SendTextMessageAsync(chatId, "На этой странице нет заявок.");
                    return;
                }

                var inlineKeyboardButtons = new List<List<InlineKeyboardButton>>();

                // Добавляем кнопки для каждой заявки
                foreach (var app in paginatedApplications)
                {
                    inlineKeyboardButtons.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData($"👤 {app.Fio} | 📞 {app.PhoneNumber} | 🛠 {app.Role}", $"application_{app.ChatId}")  // Убедитесь, что передаете уникальный ChatId
                    });
                }


                // Добавляем кнопки пагинации
                var paginationButtons = new List<InlineKeyboardButton>();
                if (page > 0)
                    paginationButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"applications_{page - 1}"));
                if ((page + 1) * pageSize < applications.Count)
                    paginationButtons.Add(InlineKeyboardButton.WithCallbackData("➡️ Вперед", $"applications_{page + 1}"));

                if (paginationButtons.Count > 0)
                    inlineKeyboardButtons.Add(paginationButtons);

                var inlineKeyboard = new InlineKeyboardMarkup(inlineKeyboardButtons);

                // Отправляем сообщение со списком заявок и кнопками
                await botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: callbackQuery.Message.MessageId,
                    text: "📋 Список заявок:\n",
                    replyMarkup: inlineKeyboard
                );
            }
            else if (data.StartsWith("application_"))
            {
                // Извлекаем ChatId из callbackData (например, "application_12345")
                long applicationChatId = long.Parse(data.Split('_')[1]);

                // Получаем информацию о заявке по ChatId
                var application = await _applicationService.GetApplicationByIdAsync(applicationChatId);

                if (application == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Заявка не найдена.");
                    return;
                }

                // Форматируем полное сообщение о заявке
                string formattedApplication = $"👤 {application.Fio}\n" +
                                              $"📞 {application.PhoneNumber}\n" +
                                              $"🛠 {application.Role}\n";

                // Создаем кнопки для одобрения и отказа
                var approveButton = InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"approve_{applicationChatId}");
                var rejectButton = InlineKeyboardButton.WithCallbackData("❌ Отказать", $"reject_{applicationChatId}");

                // Формируем клавиатуру с кнопками
                var inlineKeyboard = new InlineKeyboardMarkup(new[] { new InlineKeyboardButton[] { approveButton, rejectButton } });

                // Отправляем полную информацию о заявке с кнопками
                await botClient.SendTextMessageAsync(chatId, formattedApplication, replyMarkup: inlineKeyboard);
            }



            else if (data.StartsWith("approve_"))
            {
                long applicantChatId = long.Parse(data.Split('_')[1]);
                await botClient.SendTextMessageAsync(chatId, $"✅ Заявка пользователя {applicantChatId} одобрена.");

                // Можно добавить логику обновления статуса в базе данных
            }
            else if (data.StartsWith("reject_"))
            {
                long applicantChatId = long.Parse(data.Split('_')[1]);
                await botClient.SendTextMessageAsync(chatId, $"❌ Заявка пользователя {applicantChatId} отклонена.");

                // Можно добавить логику обновления статуса в базе данных
            }
        }
    }
}
