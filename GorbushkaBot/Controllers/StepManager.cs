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

            // Используем HashSet для уникальности
            HashSet<string> uniquePhotoIds = new HashSet<string>();

            // Сохраняем все фото (собираем FileId каждого фото)
            if (photos != null && photos.Length > 0)
            {
                if (!UserData[chatId].ContainsKey(key))
                {
                    UserData[chatId][key] = string.Empty;
                }

                foreach (var photo in photos)
                {
                    uniquePhotoIds.Add(photo.FileId); // Добавляем только уникальные FileId
                }

                // Конвертируем HashSet обратно в строку с разделением запятой
                UserData[chatId][key] = string.Join(",", uniquePhotoIds);
            }
        }



        private void ClearUserDataAfterStep(long chatId, string step)
        {
            if (!UserData.ContainsKey(chatId)) return;

            var stepsToClear = new List<string>();

            switch (step)
            {
                case "fio":
                    stepsToClear.AddRange(new[] { "passport_number", "passport_issue_date", "passport", "company_name", "company_activity", "pavilion_number", "rental_contract" });
                    break;
                case "passport_number":
                    stepsToClear.AddRange(new[] { "passport_issue_date", "passport", "company_name", "company_activity", "pavilion_number", "rental_contract" });
                    break;
                case "passport_issue_date":
                    stepsToClear.AddRange(new[] { "passport", "company_name", "company_activity", "pavilion_number", "rental_contract" });
                    break;
                case "passport":
                    stepsToClear.AddRange(new[] { "company_name", "company_activity", "pavilion_number", "rental_contract" });
                    break;
                case "role":
                    stepsToClear.AddRange(new[] { "company_name", "company_activity", "pavilion_number", "rental_contract" });
                    break;
                case "company_name":
                    stepsToClear.AddRange(new[] { "company_activity", "pavilion_number", "rental_contract" });
                    break;
                case "pavilion_number":
                    stepsToClear.AddRange(new[] { "rental_contract" });
                    break;
            }

            foreach (var key in stepsToClear)
            {
                if (UserData[chatId].ContainsKey(key))
                {
                    UserData[chatId].Remove(key);
                }
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
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport_number", "Введите номер вашего паспорта:", true);
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
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport", "Отправьте фото страниц своего паспорта, на которых находятся:\n- ФИО\n- Номер\n- Дата выдачи\n- Прописка\n\nВы можете отправить несколько фото.", true);
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
            else if (step == "company_activity" || step == "rental_contract")
            {
                if (step == "company_activity")
                    SaveUserData(chatId, "company_activity", message.Text);
                else if (step == "rental_contract")
                    SaveUserData(chatId, "rental_contract", message.Text);

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
                    // Очищаем все данные пользователя
                    if (UserData.ContainsKey(chatId))
                    {
                        UserData[chatId].Clear();
                    }
                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "fio", "Введите ваше ФИО:", false);
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
                            
                            // Редактируем сообщение о статусе
                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "⏳ Сохраняем данные...",
                                replyMarkup: null);

                            // Получаем данные пользователя
                            var userData = UserData[chatId];

                            // Создаем папку в Google Drive
                            var folderUrl = await _driveService.CreateUserFolderAsync(chatId);
                            var folderId = GetFolderIdFromUrl(folderUrl);

                            // Загружаем фотографии
                            if (userData.TryGetValue("passport_photo", out var photoIds))
                            {
                                var fileIds = photoIds.Split(',');
                                await _driveService.UploadPhotosAsync(botClient, folderId, fileIds, bottoken);
                            }

                            // Сохраняем данные в Google Sheets
                            await _sheetsService.AppendDataAsync(userData, folderUrl);

                            // Обновляем сообщение о успешной отправке
                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "✅ Заявка успешно отправлена!",
                                replyMarkup: new InlineKeyboardMarkup(new[]
                                {
                                new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") }
                                }));

                            // Очищаем данные
                            UserData.Remove(chatId);
                            UserSteps.Remove(chatId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при сохранении данных: {ex.Message}");
                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "⚠️ Ошибка при отправке данных. Попробуйте позже.");
                        }
                    }
                    break;

                case "back":
                    if (UserSteps.ContainsKey(chatId))
                    {
                        var (currentStep, currentMessageId) = UserSteps[chatId];

                        // Очищаем данные после текущего шага
                        ClearUserDataAfterStep(chatId, currentStep);

                        // Определяем предыдущий шаг
                        var previousSteps = new Dictionary<string, (string step, string message, InlineKeyboardMarkup? keyboard)>
                        {
                            { "passport_number", ("fio", "Введите ваше ФИО:", null) },
                            { "passport_issue_date", ("passport_number", "Введите номер вашего паспорта:", null) },
                            { "passport", ("passport_issue_date", "Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", null) },
                            { "role", ("passport", "Отправьте фото страниц своего паспорта, на которых находятся:\n- ФИО\n- Номер\n- Дата выдачи\n- Прописка\n\nВы можете отправить несколько фото.", null) },
                            { "pavilion_number", ("role", "Выберите свою роль:", new InlineKeyboardMarkup(new[]
                                {
                                    new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                                })) },
                            { "company_name", ("role", "Выберите свою роль:", new InlineKeyboardMarkup(new[]
                                {
                                    new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                                    new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                                })) },
                            { "company_activity", ("company_name", "Введите название вашей компании:", null) },
                            { "rental_contract", ("pavilion_number", "Введите номер павильона:", null) }
                        };

                        if (previousSteps.TryGetValue(currentStep, out var previousStepData))
                        {
                            string previousStep = previousStepData.step;
                            string previousMessage = previousStepData.message;
                            InlineKeyboardMarkup? keyboard = previousStepData.keyboard;

                            await DeleteAndSendNextStep(botClient, chatId, currentMessageId, previousStep, previousMessage, true, keyboard);
                        }
                    }
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);


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