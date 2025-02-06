using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
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
        private static readonly string bottoken = Environment.GetEnvironmentVariable("BOT_TOKEN");

        public StepManager(TelegramBotClient botClient, GoogleSheetsService sheetsService,GoogleDriveService driveService)
        {
            this.botClient = botClient;
            _sheetsService = sheetsService;
            _driveService = driveService;
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




        private void ClearUserDataAfterStep(long chatId, string step)
        {
            if (!UserData.ContainsKey(chatId)) return;

            var stepsToClear = new List<string>();

            switch (step)
            {
                case "face_photo":
                    stepsToClear.AddRange(new[] {
                    "fio", "phone_number", "passport_number", "passport_issue_date",
                    "registration_address", "passport", "role", "company_name",
                    "company_activity", "pavilion_number", "pavilion_photo", "rental_contract"
                    });
                    break;

                case "fio":
                    stepsToClear.AddRange(new[] {
                    "phone_number", "passport_number", "passport_issue_date",
                    "registration_address", "passport", "role", "company_name",
                    "company_activity", "pavilion_number", "pavilion_photo", "rental_contract"
                    });
                    break;

                case "phone_number":
                    stepsToClear.AddRange(new[] {
                    "passport_number", "passport_issue_date", "registration_address",
                    "passport", "role", "company_name", "company_activity",
                    "pavilion_number", "pavilion_photo", "rental_contract"
                    });
                    break;

                case "passport_number":
                    stepsToClear.AddRange(new[] {
                    "passport_issue_date", "registration_address", "passport",
                    "role", "company_name", "company_activity", "pavilion_number",
                    "pavilion_photo", "rental_contract"
                    });
                    break;

                case "passport_issue_date":
                    stepsToClear.AddRange(new[] {
                    "registration_address", "passport", "role", "company_name",
                    "company_activity", "pavilion_number", "pavilion_photo", "rental_contract"
                    });
                    break;

                case "registration_address":
                    stepsToClear.AddRange(new[] {
                    "passport", "role", "company_name", "company_activity",
                    "pavilion_number", "pavilion_photo", "rental_contract"
                    });
                    break;

                case "passport":
                    stepsToClear.AddRange(new[] {
                    "role", "company_name", "company_activity", "pavilion_number",
                    "pavilion_photo", "rental_contract"
                    });
                    break;

                case "role":
                    stepsToClear.AddRange(new[] {
                    "company_name", "company_activity", "pavilion_number",
                    "pavilion_photo", "rental_contract"
                    });
                    break;

                case "company_name":
                    stepsToClear.AddRange(new[] {
                    "company_activity", "pavilion_number", "pavilion_photo", "rental_contract"
                    });
                    break;

                case "pavilion_number":
                    stepsToClear.AddRange(new[] {
                    "pavilion_photo", "rental_contract"
                    });
                    break;

                case "pavilion_photo":
                    stepsToClear.AddRange(new[] { "rental_contract" });
                    break;
            }

            foreach (var key in stepsToClear.Where(UserData[chatId].ContainsKey))
            {
                UserData[chatId].Remove(key);
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

            if (step == "face_photo")
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
                    "fio",
                    "✅ Фото принято!\n\nТеперь введите ваше ФИО:",
                    true
                );
            }
            else if (step == "fio")
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
                await DeleteAndSendNextStep(botClient, chatId, messageId, "phone_number", "Введите номер вашего телефона:", true);
            }
            else if (step == "phone_number")
            {
                if (!Regex.IsMatch(message.Text, @"^\+7\d{10}$"))
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Введите корректный номер телефона (формат: +7 1234567891)."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }
                SaveUserData(chatId, "phone_number", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport_number",
                    "Введите номер вашего паспорта (формат: 0000 000000):", true);
            }
            else if (step == "passport_number")
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
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport",
                    "📷 Отправьте фото страниц своего паспорта, на которых находятся:\n\n" +
                    "• ФИО\n" +
                    "• Номер\n" +
                    "• Дата выдачи\n" +
                    "• Прописка\n\n" +
                    "Можно отправить несколько фото за раз.",
                    true);
            }
            else if (step == "passport")
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

                // Сохраняем все фото
                SaveUserPhoto(chatId, "passport_photo", message.Photo);

                // Удаляем предыдущее сообщение с инструкцией и ошибками
                await DeleteAndSendNextStep(
                    botClient,
                    chatId,
                    messageId,
                    "passport",
                    "Фото получено! Если у вас есть ещё страницы, отправьте их. Если все страницы отправлены, нажмите кнопку 'Далее'.",
                    false,
                    new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Далее", "next_after_passport") } })
                );
            }
            else if (step == "company_name")
            {
                SaveUserData(chatId, "company_name", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "company_activity", "Введите вид деятельности вашей компании:", true);
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
                    "pavilion_photo",
                    "✅ Фото принято! Если нужно добавить ещё фото, отправьте их. Иначе нажмите 'Далее'.",
                    false,
                    new InlineKeyboardMarkup(new[]
                    {
                    new[] { InlineKeyboardButton.WithCallbackData("Далее", "next_after_pavilion") }
                    })
                );
            }
            else if (step == "company_activity" )
            {
                SaveUserData(chatId, "company_activity", message.Text);

                await DeleteAndSendNextStep(botClient, chatId, messageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                        new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                    }));
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

                    // Начинаем с шага face_photo
                    await DeleteAndSendNextStep(
                        botClient,
                        chatId,
                        callbackQuery.Message.MessageId,
                        "face_photo",
                        "📸 Первый шаг: Отправьте свою фотографию (лицо крупным планом):",
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
                case "both":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "market_question", "Вы с рынка?", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Да", "market_yes") },
                            new[] { InlineKeyboardButton.WithCallbackData("Нет", "market_no") }
                        }));
                break;

                case "market_yes":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "pavilion_number", "Введите номер вашего павильона:", true);
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

                case "market_no":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "company_name", "Введите название вашей компании:", true);
                break;

                case "buyer":
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        }));
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
                            userData["passport_photos"] = $"https://drive.google.com/drive/folders/{folders["passport"]}";
                            userData["pavilion_photos"] = $"https://drive.google.com/drive/folders/{folders["pavilion"]}";

                            await _sheetsService.AppendDataAsync(userData, folders["root"]);

                            // Отправка админу заявки с кнопками одобрения/отклонения
                            long[] adminChatIds = { 8018159474, 448145168, 388009185 }; // Укажи ID админа

                            var approvalKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"approve_{chatId}") },
                                new[] { InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"reject_{chatId}") }
                            });

                            string companyName = userData.ContainsKey("company_name") ? userData["company_name"] : "Не указано";
                            string companyActivity = userData.ContainsKey("company_activity") ? userData["company_activity"] : "Не указано";

                            string adminMessage = $"📌 Новая заявка от пользователя:\n\n" +
                                $"👤 ФИО: {userData["fio"]}\n" +
                                $"📄 Паспорт: {userData["passport_number"]}, {userData["passport_issue_date"]}\n" +
                                $"🏢 Павильон: {userData["pavilion_number"]}, {userData["rental_contract"]}\n" +
                                $"🏢 Компания: {companyName}\n" +
                                $"📌 Деятельность: {companyActivity}\n\n" +
                                $"🖼 Фото: \n[Лицо]({userData["face_photo"]})\n" +
                                $"[Паспорт]({userData["passport_photos"]})\n" +
                                $"[Павильон]({userData["pavilion_photos"]})";


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

                            UserData.Remove(chatId);
                            UserSteps.Remove(chatId);
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

                    string decisionText = callbackQuery.Data.StartsWith("approve") ?
                        "✅ Ваша заявка одобрена! 🎉" :
                        "❌ Ваша заявка отклонена. Свяжитесь с поддержкой.";

                    try
                    {
                        await botClient.SendTextMessageAsync(
                            chatId: targetChatId,
                            text: decisionText);

                        await botClient.EditMessageTextAsync(
                            chatId: callbackQuery.Message.Chat.Id,
                            messageId: callbackQuery.Message.MessageId,
                            text: $"📝 Заявка пользователя {(callbackQuery.Data.StartsWith("approve") ? "одобрена ✅" : "отклонена ❌")}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при обработке заявки: {ex.Message}");
                    }

                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    break;

                case "back":
                    if (UserSteps.TryGetValue(chatId, out var currentStepData))
                    {
                        string currentStep = currentStepData.step;

                        // Определяем предыдущий шаг вручную
                        string previousStep = currentStep switch
                        {
                            "role" => "face_photo",
                            "market_question" => "role",
                            "pavilion_number" => "market_question",
                            "company_name" => "pavilion_number",
                            _ => "face_photo" // начальный шаг
                        };

                        // Тексты и клавиатуры для предыдущих шагов
                        var stepData = new Dictionary<string, (string, bool, InlineKeyboardMarkup?)>
                        {
                            { "face_photo", ("📸 Первый шаг: Отправьте свою фотографию (лицо крупным планом):", false, null) },
                            { "role", ("Выберите свою роль:", false, new InlineKeyboardMarkup(new[]
                                {
                                    new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                                }))
                            },
                            { "market_question", ("Вы с рынка?", false, new InlineKeyboardMarkup(new[]
                                {
                                    new[] { InlineKeyboardButton.WithCallbackData("Да", "market_yes") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Нет", "market_no") }
                                }))
                            },
                            { "pavilion_number", ("Введите номер вашего павильона:", true, null) },
                            { "company_name", ("Введите название вашей компании:", true, null) },
                        };

                        if (stepData.TryGetValue(previousStep, out var stepInfo))
                        {
                            await DeleteAndSendNextStep(botClient, chatId, currentStepData.messageId, previousStep, stepInfo.Item1, stepInfo.Item2, stepInfo.Item3);

                            // Обновляем текущий шаг
                            UserSteps[chatId] = (previousStep, currentStepData.messageId);
                        }
                    }
                    break;



            }
        }

        private string GetFolderIdFromUrl(string url)
        {
            var parts = url.Split(new[] { "folders/" }, StringSplitOptions.None);
            return parts.Length > 1 ? parts[1].Split('?')[0] : null;
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
            if (nextStep != "fio" && keyboard == null)
            {
                keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
                });
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