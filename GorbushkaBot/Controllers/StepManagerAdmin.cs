using Google.Apis.Drive.v3.Data;
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
        private readonly UserAcceptService _userAcceptService;
        private readonly GoogleSheetsService _googleSheetsService;

        public StepManagerAdmin(ApplicationService applicationService, UserAcceptService userAcceptService, GoogleSheetsService googleSheetsService)
        {
            _applicationService = applicationService;
            _userAcceptService = userAcceptService;
            _googleSheetsService = googleSheetsService;
        }

        public void SaveStep(long chatId, string step, int messageId)
        {
            AdminSteps[chatId] = (step, messageId);
        }

        public async Task HandleMessage(ITelegramBotClient botClient, long chatId, Message message)
        {
            if (!AdminSteps.ContainsKey(chatId))
            {
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
            else if (data.StartsWith("applications_"))
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

                await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "📋 Список заявок:\n",
                replyMarkup: inlineKeyboard);

            }
            else if (data.StartsWith("update_categories"))
            {
                // Вызов метода для закрепления сообщения с кнопками
                var success = await _applicationService.PinMessageWithLinksAsync();

                if (success)
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Категории обновлены!");
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ошибка при обновлении категорий.");
                }
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
                string formattedApplication = $"👤 ФИО: {application.Fio}\n" +
                                              $"📛 Ник тг: {application.UserNameTg}\n" +
                                              $"📄 Паспорт:{application.PassportNumber}, {application.PassportIssueDate}\n" +
                                              $"📞 Телефон (контактный): {application.PhoneNumber}\n" +
                                              $"🛠 Роль: {application.Role}\n" +
                                              $"🏢 Павильон: {application.PavilionNumber}, {application.RentalContract}\n" +
                                              $"🏛 Номер союза: {application.NomerSoiza}\n" +
                                              $"🖼 Фото: \n Лицо: {application.FacePhoto}\n Паспорт: {application.PassportPhotos}\n Прописка: {application.PropiskaPhoto}\n Павильон: {application.PavilionPhotos}";
                                              
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

                // 1. Извлекаем данные из таблицы UserApplications
                var application = await _applicationService.GetApplicationByIdAsync(applicantChatId);
                string telegramUsername = application.UserNameTg ?? "Не указано";

                if (application == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Заявка не найдена.");
                    return;
                }

                // 2. Проверка, что FolderUrl не null
                if (string.IsNullOrEmpty(application.FolderUrl))
                {
                    await botClient.SendTextMessageAsync(chatId, "Не найдена ссылка на папку в Google Drive.");
                    return;
                }

                var folderUrl = application.FolderUrl; // Ссылка на папку в Google Drive

                // 3. Проверка на null для других полей заявки
                if (application.FacePhoto == null || application.Fio == null || application.PhoneNumber == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Некоторые обязательные данные заявки отсутствуют.");
                    return;
                }

                // 4. Переносим данные из заявки в новый объект для сохранения в таблице UserAccepts
                var userData = new Dictionary<string, string>
                {
                    { "face_photo", application.FacePhoto },
                    { "fio", application.Fio },
                    { "phone_number", application.PhoneNumber },
                    { "passport_number", application.PassportNumber ?? "" }, // Обработка возможных null значений
                    { "role", application.Role ?? "" },
                    { "passport_issue_date", application.PassportIssueDate ?? "" },
                    { "registration_address", application.RegistrationAddress ?? "" },
                    { "passport_photo", application.PassportPhotos ?? "" },
                    { "pavilion_number", application.PavilionNumber ?? "" },
                    { "rental_contract", application.RentalContract ?? "" },
                    { "pavilion_photo", application.PavilionPhotos ?? "" },
                    { "soiuz_number", application.NomerSoiza ?? "" }
                };

                // 5. Сохраняем данные в Google Sheets
                await _googleSheetsService.AppendUserDataAsync(userData, folderUrl, applicantChatId, telegramUsername);

                await _userAcceptService.SaveUserAcceptAsync(userData, folderUrl, applicantChatId, telegramUsername);

                // 6. Удаляем заявку из таблицы UserApplications
                await _applicationService.DeleteApplicationAsync(applicantChatId);




                // 8. Отправляем сообщение пользователю о том, что его заявка одобрена
                await botClient.SendTextMessageAsync(applicantChatId, "Ваша заявка была одобрена! Ожидайте добавление в чат ✅");
            }
            else if (data.StartsWith("reject_"))
            {
                long applicantChatId = long.Parse(data.Split('_')[1]);

                await _applicationService.DeleteApplicationAsync(applicantChatId);

                // Создаем кнопку "Заполнить заново"
                var keyboard = new InlineKeyboardMarkup(
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("Заполнить заново 🔄", "verify")
                    });

                // Отправляем сообщение с кнопкой
                await botClient.SendTextMessageAsync(
                    applicantChatId,
                    "К сожалению, ваша заявка была отклонена. ❌\nВы можете подать заявку заново, нажав кнопку ниже.",
                    replyMarkup: keyboard
                );
            }
            else if (data == "black_list")
            {
                await _googleSheetsService.ProcessBlackListAsync();
            }


        }
    }
}
