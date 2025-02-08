using GorbushkaBot.AppDbContext;
using GorbushkaBot.Service;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class StepManager
    {
        private static readonly Dictionary<long, (string step, int messageId)> UserSteps = new();
        private static readonly Dictionary<long, Dictionary<string, string>> UserData = new();
        private static readonly Dictionary<long, (int errorMsgId, int userMsgId)> LastErrorMessages = new(); // Новый словарь для ошибок
        private readonly TelegramBotClient botClient;
        private readonly GoogleSheetsService _sheetsService;
        private readonly GoogleDriveService _driveService;
        private readonly UserApplicationService _userApplicationService;
        private static readonly string bottoken = Environment.GetEnvironmentVariable("BOT_TOKEN");

        public StepManager(TelegramBotClient botClient, GoogleSheetsService sheetsService,GoogleDriveService driveService, UserApplicationService userApplicationService)
        {
            this.botClient = botClient;
            _sheetsService = sheetsService;
            _driveService = driveService;
            _userApplicationService = userApplicationService;
        }

        public void SaveStep(long chatId, string step, int messageId)
        {
            UserSteps[chatId] = (step, messageId);
        }

        private void SaveUserData(long chatId, string key, string value)
        {
            if (!UserData.ContainsKey(chatId))
            {
                UserData[chatId] = new Dictionary<string, string>();
            }
            UserData[chatId][key] = value;
        }

        private void SaveUserPhoto(long chatId, string key, PhotoSize[] photos)
        {
            if (!UserData.ContainsKey(chatId))
            {
                UserData[chatId] = new Dictionary<string, string>();
            }

            // Загружаем уже сохраненные фото
            HashSet<string> uniquePhotoIds = new HashSet<string>();

            if (UserData[chatId].ContainsKey(key) && !string.IsNullOrEmpty(UserData[chatId][key]))
            {
                var existingPhotoIds = UserData[chatId][key].Split(',');
                foreach (var id in existingPhotoIds)
                {
                    uniquePhotoIds.Add(id);
                }
            }

            // Добавляем ТОЛЬКО самое большое изображение каждого фото
            if (photos != null && photos.Length > 0)
            {
                var largestPhoto = photos.OrderByDescending(p => p.FileSize).FirstOrDefault(); // Берем только самое большое

                if (largestPhoto != null)
                {
                    uniquePhotoIds.Add(largestPhoto.FileId);
                }

                // Сохраняем обратно в UserData
                UserData[chatId][key] = string.Join(",", uniquePhotoIds);
            }
        }

        public async Task HandleMessage(ITelegramBotClient botClient, long chatId, Message message)
        {
            if (!UserSteps.ContainsKey(chatId)) return;

            var (step, messageId) = UserSteps[chatId];

            if (LastErrorMessages.TryGetValue(chatId, out var lastError))
            {
                try { await botClient.DeleteMessageAsync(chatId, lastError.errorMsgId); } catch { }
                try { await botClient.DeleteMessageAsync(chatId, lastError.userMsgId); } catch { }
                LastErrorMessages.Remove(chatId);
            }

            if (step == "fio")
            {
                if (!Regex.IsMatch(message.Text, "^[А-Яа-яA-Za-z ]+$"))
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: ФИО должно содержать только буквы и пробелы."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }
                SaveUserData(chatId, "fio", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "face_photo", "📷 Отправьте фото лица:", true);
            }
            else if (step == "face_photo")
            {
                if (message.Photo == null)
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Пожалуйста, отправьте именно фотографию."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }

                SaveUserPhoto(chatId, "face_photo", message.Photo);
                await DeleteAndSendNextStep(
                    botClient,
                    chatId,
                    messageId,
                    "phone_number",
                    "✅ Фото принято!\n\nТеперь введите ваш номер телефона:",
                    true
                );
            }
            else if (step == "phone_number")
            {
                if (!Regex.IsMatch(message.Text, @"^\+\d{10}$"))
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Введите корректный номер телефона (формат: +71234567891)."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }
                SaveUserData(chatId, "phone_number", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "role", "Выберите свою роль:", false,
                    new InlineKeyboardMarkup(new[]
                    {
                new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                    }));
            }
            else if (step == "citizenship")
            {
                // Этот шаг обрабатывается в HandleCallbackQuery
            }
            else if (step == "passport_rus")
            {
                if (message.Photo == null)
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Отправьте фото первой страницы паспорта."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }

                SaveUserPhoto(chatId, "passport_photo", message.Photo);
                await DeleteAndSendNextStep(
                    botClient,
                    chatId,
                    messageId,
                    "passport_rus_data",
                    "✅ Фото принято!\n\nТеперь введите номер паспорта (формат: 0000 000000):",
                    true
                );
            }
            else if (step == "passport_other")
            {
                if (message.Photo == null)
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Отправьте фото первой страницы паспорта."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }

                SaveUserPhoto(chatId, "passport_photo", message.Photo);
                await DeleteAndSendNextStep(
                    botClient,
                    chatId,
                    messageId,
                    "passport_rus_data",
                    "✅ Фото принято!\n\nТеперь введите номер паспорта (формат: 0000 000000):",
                    true
                );
            }
            else if (step == "passport_rus_data")
            {
                if (!Regex.IsMatch(message.Text, @"^\d{4} \d{6}$"))
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Введите корректный номер паспорта (формат: 0000 000000)."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }
                SaveUserData(chatId, "passport_number", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport_issue_date", "Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true);
            }
            else if (step == "passport_issue_date")
            {
                if (!Regex.IsMatch(message.Text, @"^\d{2}\.\d{2}\.\d{4}$"))
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Введите корректную дату в формате ДД.ММ.ГГГГ."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }
                SaveUserData(chatId, "passport_issue_date", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "registration_address", "Введите свой адрес прописки:", true);
            }
            else if (step == "registration_address")
            {
                SaveUserData(chatId, "registration_address", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "pavilion_number", "Введите номер вашего павильона:", true);
            }
            else if (step == "pavilion_number")
            {
                SaveUserData(chatId, "pavilion_number", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "rental_contract", "Введите номер вашего договора аренды:", true);
            }
            else if (step == "rental_contract")
            {
                SaveUserData(chatId, "rental_contract", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "pavilion_photo", "📷 Отправьте фото вашего павильона:", true);
            }
            else if (step == "pavilion_photo")
            {
                if (message.Photo == null)
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Отправьте именно фото, а не текст или документ."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }

                SaveUserPhoto(chatId, "pavilion_photo", message.Photo);
                await DeleteAndSendNextStep(
                    botClient,
                    chatId,
                    messageId,
                    "completed",
                    "✅ Фото принято! Заявка заполнена.\n\nВыберите действие:",
                    false,
                    new InlineKeyboardMarkup(new[]
                    {
                new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                    })
                );
            }   
        }

        public async Task HandleCallbackQuery(ITelegramBotClient botClient, long chatId, CallbackQuery callbackQuery)
        {
            string data = callbackQuery.Data;

            if (LastErrorMessages.TryGetValue(chatId, out var lastError))
            {
                try { await botClient.DeleteMessageAsync(chatId, lastError.errorMsgId); } catch { }
                try { await botClient.DeleteMessageAsync(chatId, lastError.userMsgId); } catch { }
                LastErrorMessages.Remove(chatId);
            }

            switch (data)
            {
                case "verify":
                    // Очищаем предыдущие данные
                    if (UserData.ContainsKey(chatId)) UserData[chatId].Clear();

                    // Начинаем с шага fio
                    await DeleteAndSendNextStep(
                        botClient,
                        chatId,
                        callbackQuery.Message.MessageId,
                        "fio",
                        "Введите ваше ФИО:",
                        false
                    );
                break;

                case "next_after_passport":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "role", "Выберите свою роль:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                            new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                            new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                        }));
                break;

                case "seller":
                case "buyer":
                case "both":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "citizenship", "Уточните ваше гражданство:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                    new[] { InlineKeyboardButton.WithCallbackData("РФ", "passport_rus") },
                    new[] { InlineKeyboardButton.WithCallbackData("Другое", "passport_other") }
                        }));
                    break;

                case "passport_rus":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "passport_rus", "📷 Отправьте фото первой страницы паспорта:", true);
                    break;

                case "passport_other":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "passport_other", "📷 Отправьте фото первой страницы паспорта:", true);
                    break;

                case "next_after_pavilion":
                    await DeleteAndSendNextStep(
                        botClient,
                        chatId,
                        callbackQuery.Message.MessageId,
                        "completed",
                        "Заявка заполнена.\n\nВыберите действие:",
                        false,
                        new InlineKeyboardMarkup(new[]
                        {
                        new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                        new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        })
                    );
                break;


                case "submit":
                    if (UserData.ContainsKey(chatId))
                    {
                        try
                        {
                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "⏳ Сохраняем данные...",
                                replyMarkup: null);

                            var userData = UserData[chatId];

                            // Создаем папки и получаем их ID
                            var folders = await _driveService.CreateUserFolderAsync(chatId);

                            // Загружаем фото лица
                            if (userData.TryGetValue("face_photo", out var facePhotoId))
                            {
                                await _driveService.UploadPhotosAsync(
                                    botClient,
                                    folders["face"],
                                    new[] { facePhotoId.Split(',')[0] }
                                );
                            }

                            // Загружаем фото паспорта
                            if (userData.TryGetValue("passport_photo", out var passportPhotos))
                            {
                                await _driveService.UploadPhotosAsync(
                                    botClient,
                                    folders["passport"],
                                    passportPhotos.Split(',')
                                );
                            }

                            // Загружаем фото павильона
                            if (userData.TryGetValue("pavilion_photo", out var pavilionPhotos))
                            {
                                await _driveService.UploadPhotosAsync(
                                    botClient,
                                    folders["pavilion"],
                                    pavilionPhotos.Split(',')
                                );
                            }
                            
                            // Обновляем данные для таблицы
                            userData["face_photo"] = $"https://drive.google.com/drive/folders/{folders["face"]}";
                            userData["passport_photo"] = $"https://drive.google.com/drive/folders/{folders["passport"]}";
                            userData["pavilion_photo"] = $"https://drive.google.com/drive/folders/{folders["pavilion"]}";

                            await _sheetsService.AppendDataAsync(userData, folders["root"]);
                            await _userApplicationService.SaveUserApplicationAsync(userData, folders["root"],chatId);

                            // Отправка админу заявки с кнопками одобрения/отклонения
                            long[] adminChatIds = { 8018159474, 448145168, 388009185, 7069858455 }; // Укажи ID админа

                            var approvalKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"approve_{chatId}") },
                                new[] { InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"reject_{chatId}") }
                            });

                            string fio = userData.ContainsKey("fio") ? userData["fio"] : "Не указано";
                            string passportNumber = userData.ContainsKey("passport_number") ? userData["passport_number"] : "Не указано";
                            string passportIssueDate = userData.ContainsKey("passport_issue_date") ? userData["passport_issue_date"] : "Не указано";
                            string pavilionNumber = userData.ContainsKey("pavilion_number") ? userData["pavilion_number"] : "Не указано";
                            string rentalContract = userData.ContainsKey("rental_contract") ? userData["rental_contract"] : "Не указано";
                            string facePhoto = userData.ContainsKey("face_photo") ? userData["face_photo"] : "Не указано";
                            string passportphotos = userData.ContainsKey("passport_photo") ? userData["passport_photo"] : "Не указано";
                            string pavilionphotos = userData.ContainsKey("pavilion_photo") ? userData["pavilion_photo"] : "Не указано";

                            string adminMessage = $"📌 Новая заявка от пользователя:\n\n" +
                                $"👤 ФИО: {fio}\n" +
                                $"📄 Паспорт: {passportNumber}, {passportIssueDate}\n" +
                                $"🏢 Павильон: {pavilionNumber}, {rentalContract}\n" +
                                $"🖼 Фото: \n[Лицо]({facePhoto})\n" +
                                $"[Паспорт]({passportPhotos})\n" +
                                $"[Павильон]({pavilionPhotos})";

                            // Отправка сообщения всем администраторам
                            foreach (var adminChatId in adminChatIds)
                            {
                                await botClient.SendTextMessageAsync(
                                    chatId: adminChatId,
                                    text: adminMessage,
                                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                                    replyMarkup: approvalKeyboard);
                            }

                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "✅ Заявка успешно отправлена! Ожидайте подтверждения.",
                                replyMarkup: new InlineKeyboardMarkup(new[]
                                {
                                    new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") }
                                }));

                            
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка: {ex}");
                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "⚠️ Ошибка при отправке. Попробуйте позже.");
                        }
                    }
                break;

                case "approve":
                case "reject":
                    string[] parts = callbackQuery.Data.Split('_');

                    if (parts.Length < 2 || !long.TryParse(parts[1], out long targetChatId))
                    {
                        Console.WriteLine($"Ошибка: некорректные данные callback: {callbackQuery.Data}");
                        return;
                    }

                    // Загружаем данные заявки из базы
                    using (var dbContext = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>()))
                    {
                        var userApplication = await dbContext.UserApplications
                            .FirstOrDefaultAsync(u => u.ChatId == targetChatId);

                        if (userApplication == null)
                        {
                            Console.WriteLine($"Ошибка: заявка пользователя с chatId {targetChatId} не найдена в базе данных.");
                            return;
                        }

                        string decisionText = callbackQuery.Data.StartsWith("approve")
                            ? $"✅ Ваша заявка одобрена! 🎉\n\n**Данные:**\nФИО: {userApplication.Fio}\nТелефон: {userApplication.PhoneNumber}"
                            : $"❌ Ваша заявка отклонена. Свяжитесь с поддержкой.\n\n**Данные:**\nФИО: {userApplication.Fio}\nТелефон: {userApplication.PhoneNumber}";

                        try
                        {
                            await botClient.SendTextMessageAsync(
                                chatId: targetChatId,
                                text: decisionText);

                            await botClient.EditMessageTextAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: $"📝 Заявка пользователя {(callbackQuery.Data.StartsWith("approve") ? "одобрена ✅" : "отклонена ❌")}\n\nФИО: {userApplication.Fio}\nТелефон: {userApplication.PhoneNumber}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при обработке заявки: {ex.Message}");
                        }
                    }

                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    break;


                case "back":
                    if (UserSteps.TryGetValue(chatId, out var currentStepData))
                    {
                        string currentStep = currentStepData.step;
                        string previousStep = null;

                        // Модифицируем порядок шагов с учётом новых условий
                        var stepOrder = new List<string>
                        {
                            "fio", "face_photo", "phone_number","role","citizenship","passport_number", "passport_issue_date",
                            "registration_address", "passport","pavilion_number", "rental_contract", "pavilion_photo"
                        };

                        // Если текущий шаг - "pavilion_number", мы должны пропустить шаги "company_name", "company_activity"
                        if (currentStep == "pavilion_number")
                        {
                            // Найдём индекс для шага "market_question" и используем его как предыдущий шаг
                            previousStep = "market_question";
                        }
                        else
                        {
                            int currentIndex = stepOrder.IndexOf(currentStep);
                            if (currentIndex > 0)
                                previousStep = stepOrder[currentIndex - 1];
                        }

                        if (previousStep != null)
                        {
                            var stepData = new Dictionary<string, (string, bool, InlineKeyboardMarkup?)>
                            {
                                { "fio", ("Введите ваше ФИО:", true, null) },
                                { "face_photo", ("📸 Первый шаг: Отправьте свою фотографию (лицо крупным планом):", false, null) },
                                { "phone_number", ("Введите номер вашего телефона:", true, null) },
                                { "passport_number", ("Введите номер вашего паспорта (формат: 0000 000000):", true, null) },
                                { "passport_issue_date", ("Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true, null) },
                                { "registration_address", ("Введите свой адрес прописки:", true, null) },
                                { "passport", ("📷 Отправьте фото паспорта с данными:", false, null) },
                                { "role", ("Выберите свою роль:", false,
                                    new InlineKeyboardMarkup(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                                    })) },
                                { "pavilion_number", ("Введите номер вашего павильона:", true, null) },
                                { "rental_contract", ("Введите номер договора аренды:", true, null) },
                                { "pavilion_photo", ("📷 Отправьте фото вашего павильона:", false, null) }
                            };

                            if (stepData.TryGetValue(previousStep, out var stepInfo))
                            {
                                // Примечание: Мы передаем keyboard в метод DeleteAndSendNextStep
                                await DeleteAndSendNextStep(botClient, chatId, currentStepData.messageId, previousStep, stepInfo.Item1, stepInfo.Item2, stepInfo.Item3);
                            }
                        }

                    }
                    break;

            }
        }

        private async Task DeleteAndSendNextStep(ITelegramBotClient botClient, long chatId, int messageId, string nextStep, string messageText, bool isTextInput, InlineKeyboardMarkup? keyboard = null)
        {
            // Удаляем предыдущее сообщение
            try
            {
                await botClient.DeleteMessageAsync(chatId, messageId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении сообщения: {ex.Message}");
            }

            // Добавляем кнопку "Назад", если это не начальный шаг
            if (nextStep != "role" && nextStep != "completed") // Назад добавляется всегда, кроме первого шага и шага выбора роли
            {
                var backButton = InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back");
                if (keyboard == null)
                {
                    keyboard = new InlineKeyboardMarkup(new[] { new[] { backButton } });
                }
                else
                {
                    var buttons = keyboard.InlineKeyboard.ToList();
                    buttons.Add(new[] { backButton });
                    keyboard = new InlineKeyboardMarkup(buttons);
                }
            }

            // Отправляем новое сообщение
            var newMessage = await botClient.SendTextMessageAsync(
                chatId,
                messageText,
                replyMarkup: keyboard
            );

            // Сохраняем новый messageId для следующего шага
            SaveStep(chatId, nextStep, newMessage.MessageId);
        }

    }
}