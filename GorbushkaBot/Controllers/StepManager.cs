using GorbushkaBot.AppDbContext;
using GorbushkaBot.Model;
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
        long[] adminChatIds = { 8018159474, 448145168, 388009185, 7069858455 };
        private static readonly Dictionary<long, (string step, int messageId)> UserSteps = new();
        private static readonly Dictionary<long, Dictionary<string, string>> UserData = new();

        private static readonly Dictionary<long, (int errorMsgId, int userMsgId)> LastErrorMessages = new(); // Новый словарь для ошибок
        private readonly TelegramBotClient botClient;
        private readonly GoogleSheetsService _sheetsService;
        private readonly GoogleDriveService _driveService;
        private readonly UserApplicationService _userApplicationService;
        private readonly UserAcceptService _userAcceptService;
        private readonly ApplicationDbContext _dbContext;
        private static readonly string bottoken = Environment.GetEnvironmentVariable("BOT_TOKEN");

        public StepManager(TelegramBotClient botClient, GoogleSheetsService sheetsService,GoogleDriveService driveService, UserApplicationService userApplicationService, ApplicationDbContext dbContext, UserAcceptService userAcceptService)
        {
            this.botClient = botClient;
            _sheetsService = sheetsService;
            _driveService = driveService;
            _userApplicationService = userApplicationService;
            _userAcceptService = userAcceptService;
            _dbContext = dbContext;
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
                    "✅ Фото принято!\n\nТеперь введите КОНТАКТНЫЙ, номер телефона:",
                    true
                );
            }
            else if (step == "phone_number")
            {
                if (!Regex.IsMatch(message.Text, @"^\+\d+$"))
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
                    "propiska_photo",
                    "✅ Фото принято!\n\nТеперь отправьте фото прописки :",
                    true
                );
            }
            else if (step == "propiska_photo")
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
                    "passport_other_data",
                    "✅ Фото принято!\n\nТеперь введите номер паспорта :",
                    true
                );
            }
            else if (step == "passport_rus_data")
            {
                if (!Regex.IsMatch(message.Text, @"^\d{4} \d{6}$"))
                {
                    var errorMsg = await botClient.SendTextMessageAsync(
                        chatId,
                        "Ошибка: Введите корректный номер паспорта (формат: 0000 000000 или с буквами и пробелами)."
                    );
                    LastErrorMessages[chatId] = (errorMsg.MessageId, message.MessageId);
                    return;
                }
                SaveUserData(chatId, "passport_number", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport_issue_date_rus", "Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true);
            }
            else if (step == "passport_issue_date_rus")
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
            else if (step == "passport_other_data")
            {
                SaveUserData(chatId, "passport_number", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport_issue_date_other", "Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true);
            }
            else if (step == "passport_issue_date_other")
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
                await DeleteAndSendNextStep(botClient, chatId, messageId, "passport_issue_date_end", "Введите дату до какого числа действует:", true);
            }
            else if (step == "passport_issue_date_end")
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
                SaveUserData(chatId, "passport_issue_date_end", message.Text);
                await DeleteAndSendNextStep(botClient, chatId, messageId, "pavilion_number", "Введите номер вашего павильона:", true);
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
                await DeleteAndSendNextStep(botClient, chatId, messageId, "pavilion_photo", "📷 Отправьте фото вашего договора:", true);
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

                // Получаем сохраненные данные пользователя
                var userData = UserData[chatId];

                string fio = userData["fio"];
                string passportNumber = userData["passport_number"];
                string passportIssueDate = userData["passport_issue_date"];
                string passpirtIssueDateEnd = userData["passport_issue_date_end"];
                string phoneNumber = userData["phone_number"];
                string role = userData["role"];
                string pavilionNumber = userData["pavilion_number"];
                string rentalContract = userData["rental_contract"];
                string facePhoto = userData["face_photo"];
                string passportPhotos = userData["passport_photo"];
                string propiskaPhotos = userData["propiska_photo"];
                string pavilionPhotos = userData["pavilion_photo"];
                string registration_address = userData["registration_address"];

                string completedMessage = $"✅ <b>Заявка заполнена!</b>\n\n" +
                    $"👤 <b>ФИО:</b> {fio}\n" +
                    $"📄 <b>Паспорт:</b> {passportNumber}, {passportIssueDate}, {passpirtIssueDateEnd} \n" +
                    $"🏢 <b>Адрес прописки:</b> {registration_address}\n" +
                    $"📞 <b>Телефон (контактный):</b> {phoneNumber}\n" +
                    $"💼 <b>Роль:</b> {role}\n" +
                    $"🏢 <b>Павильон:</b> {pavilionNumber}, {rentalContract}\n" +
                    $"🖼 <b>Фото:</b> ниже ⬇️";

                // Отправляем текст с данными
                await botClient.SendTextMessageAsync(
                    chatId,
                    completedMessage,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
                );

                // Собираем список фотографий
                var mediaList = new List<IAlbumInputMedia>();

                if (!string.IsNullOrEmpty(passportPhotos))
                    mediaList.Add(new InputMediaPhoto(new InputFileId(passportPhotos)));

                if (!string.IsNullOrEmpty(propiskaPhotos))
                    mediaList.Add(new InputMediaPhoto(new InputFileId(propiskaPhotos)));

                if (!string.IsNullOrEmpty(pavilionPhotos))
                    mediaList.Add(new InputMediaPhoto(new InputFileId(pavilionPhotos)));

                if (!string.IsNullOrEmpty(facePhoto))
                    mediaList.Add(new InputMediaPhoto(new InputFileId(facePhoto)));

                // Отправляем все фото одним сообщением, если есть хотя бы одно фото
                if (mediaList.Count > 0)
                {
                    await botClient.SendMediaGroupAsync(chatId, mediaList);

                    // Отправляем кнопки
                    await DeleteAndSendNextStep(botClient, chatId, messageId, "completed", "Выберите действие:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        }));
                }


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

                    SaveUserData(chatId, "role", "Продавец");

                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "citizenship", "Уточните ваше гражданство:", false,
                       new InlineKeyboardMarkup(new[]
                       {
                            new[] { InlineKeyboardButton.WithCallbackData("РФ", "passport_rus") },
                            new[] { InlineKeyboardButton.WithCallbackData("Другое", "passport_other") }
                       }));
                    break;
                case "buyer":
                    
                    SaveUserData(chatId, "role", "Покупатель");

                    await DeleteAndSendNextStep(botClient, chatId, callbackQuery.Message.MessageId, "citizenship", "Уточните ваше гражданство:", false,
                       new InlineKeyboardMarkup(new[]
                       {
                            new[] { InlineKeyboardButton.WithCallbackData("РФ", "passport_rus") },
                            new[] { InlineKeyboardButton.WithCallbackData("Другое", "passport_other") }
                       }));
                    break;
                case "both":
                    
                    SaveUserData(chatId, "role", "Продавец и Покупатель"); 

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

                            await _sheetsService.AppendDataAsync(userData, folders["root"],chatId);
                            await _userApplicationService.SaveUserApplicationAsync(userData, folders["root"],chatId);


                            string fio = userData.ContainsKey("fio") ? userData["fio"] : "Не указано";
                            string passportNumber = userData.ContainsKey("passport_number") ? userData["passport_number"] : "Не указано";
                            string role = userData.ContainsKey("role") ? userData["role"] : "Не указано";
                            string phoneNumber = userData.ContainsKey("phone_number") ? userData["phone_number"] : "Не указано";
                            string passportIssueDate = userData.ContainsKey("passport_issue_date") ? userData["passport_issue_date"] : "Не указано";
                            string passportIssueDateEnd = userData.ContainsKey("passport_issue_date_end") ? userData["passport_issue_date_end"] : "Не указано";
                            string pavilionNumber = userData.ContainsKey("pavilion_number") ? userData["pavilion_number"] : "Не указано";
                            string rentalContract = userData.ContainsKey("rental_contract") ? userData["rental_contract"] : "Не указано";
                            string facePhoto = userData.ContainsKey("face_photo") ? userData["face_photo"] : "Не указано";
                            string passportphotos = userData.ContainsKey("passport_photo") ? userData["passport_photo"] : "Не указано";
                            string propiskaphoto = userData.ContainsKey("propiska_photo") ? userData["propiska_photo"] : "Не указано";
                            string pavilionphotos = userData.ContainsKey("pavilion_photo") ? userData["pavilion_photo"] : "Не указано";
                            string registration_address = userData.ContainsKey("registration_address") ? userData["registration_address"] : "Не указано";


                            await botClient.EditMessageTextAsync(
                                chatId: chatId,
                                messageId: callbackQuery.Message.MessageId,
                                text: "✅ Заявка успешно отправлена! Ожидайте подтверждения.");

                            
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
                case "back":
                    if (UserSteps.TryGetValue(chatId, out var currentStepData))
                    {
                        string currentStep = currentStepData.step;
                        string previousStep = GetPreviousStep(currentStep);

                        if (previousStep != null)
                        {
                            var stepData = new Dictionary<string, (string, bool, InlineKeyboardMarkup?)>
                            {
                                { "fio", ("Введите ваше ФИО:", true, null) },
                                { "face_photo", ("📸 Первый шаг: Отправьте свою фотографию (лицо крупным планом):", false, null) },
                                { "phone_number", ("Введите номер вашего телефона:", true, null) },
                                { "role", ("Выберите свою роль:", false,
                                    new InlineKeyboardMarkup(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                                    })) },
                                { "citizenship", ("Уточните ваше гражданство:", false,
                                    new InlineKeyboardMarkup(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithCallbackData("РФ", "passport_rus") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Другое", "passport_other") }
                                    })) },
                                { "passport_rus", ("📷 Отправьте фото первой страницы паспорта:", true, null) },
                                { "propiska_photo", ("📷 Отправьте фото страницы с пропиской:", true, null) },
                                { "passport_other", ("📷 Отправьте фото первой страницы паспорта:", true, null) },
                                { "passport_rus_data", ("Введите номер паспорта (формат: 0000 000000):", true, null) },
                                { "passport_other_data", ("Введите номер паспорта:", true, null) },
                                { "passport_issue_date_rus", ("Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true, null) },
                                { "passport_issue_date_other", ("Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true, null) },
                                { "passport_issue_date_end", ("Введите дату до какого числа действует (в формате ДД.ММ.ГГГГ):", true, null) },
                                { "registration_address", ("Введите свой адрес прописки:", true, null) },
                                { "pavilion_number", ("Введите номер вашего павильона:", true, null) },
                                { "rental_contract", ("Введите номер вашего договора аренды:", true, null) },
                                { "pavilion_photo", ("📷 Отправьте фото вашего договора:", false, null) }
                            };

                            if (stepData.TryGetValue(previousStep, out var stepInfo))
                            {
                                await DeleteAndSendNextStep(botClient, chatId, currentStepData.messageId, previousStep, stepInfo.Item1, stepInfo.Item2, stepInfo.Item3);
                            }
                        }
                    }
                    break;

            }
        }

        private string GetPreviousStep(string currentStep)
        {
            var stepOrder = new Dictionary<string, string>
            {
                { "fio", null },
                { "face_photo", "fio" },
                { "phone_number", "face_photo" },
                { "role", "phone_number" },
                { "citizenship", "role" },
                { "passport_rus", "citizenship" },
                { "propiska_photo", "passport_rus"},
                { "passport_other", "citizenship" },
                { "passport_rus_data", "propiska_photo" },
                { "passport_other_data", "passport_other" },
                { "passport_issue_date_rus", "passport_rus_data" },
                { "passport_issue_date_other", "passport_other_data" },
                { "passport_issue_date_end", "passport_issue_date_other"},
                { "registration_address", "passport_issue_date_rus" },
                { "pavilion_number", "registration_address" },
                { "rental_contract", "pavilion_number" },
                { "pavilion_photo", "rental_contract" }
            };

            return stepOrder.TryGetValue(currentStep, out var previousStep) ? previousStep : null;
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
            if (nextStep != "fio" && nextStep != "completed") // Назад добавляется всегда, кроме первого шага и шага выбора роли
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