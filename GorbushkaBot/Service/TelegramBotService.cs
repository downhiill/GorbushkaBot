using GorbushkaBot.Controllers;
using GorbushkaBot.Service;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using GorbushkaBot.AppDbContext;

public class TelegramBotService
{
    private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN")
        ?? throw new InvalidOperationException("BOT_TOKEN is not set");

    private static readonly string CredentialPath = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_PATH")
        ?? throw new InvalidOperationException("GOOGLE_CREDENTIALS_PATH is not set");

    private static readonly string SpreadsheetId = Environment.GetEnvironmentVariable("GOOGLE_SHEET_ID")
        ?? throw new InvalidOperationException("GOOGLE_SHEET_ID is not set");
    private static readonly string SpreedsheetCategoriesId = "1UovBQNNaA5sEKu9AhyZTqjJq9ORd_AlwKgK5x4525pI";

    private readonly TelegramBotClient botClient;
    private readonly StepManager stepManager;
    private readonly KeyboardManager keyboardManager;
    private readonly ErrorHandler errorHandler;
    private readonly GoogleSheetsService googleSheetsService;
    private readonly GoogleDriveService googleDriveService;
    private readonly UserApplicationService userApplicationService;
    private readonly UserAcceptService userAcceptService;
    private readonly ApplicationDbContext applicationDbContext;
    private readonly ApplicationService applicationService;
    private readonly StepManagerAdmin stepManagerAdmin;


    long[] adminIds = { 8018159474, 448145168, 388009185, 7069858455 }; // Список ID администраторов

    public TelegramBotService(UserApplicationService userApplicationService, ApplicationService applicationService, UserAcceptService userAcceptService, ApplicationDbContext applicationDbContext)
    {
        botClient = new TelegramBotClient(BotToken);
        googleSheetsService = new GoogleSheetsService(CredentialPath, SpreadsheetId, SpreedsheetCategoriesId, userApplicationService, userAcceptService);
        googleDriveService = new GoogleDriveService();
        stepManager = new StepManager(botClient, googleSheetsService, googleDriveService, userApplicationService, applicationDbContext, userAcceptService);
        stepManagerAdmin = new StepManagerAdmin(applicationService, userAcceptService, googleSheetsService);
        keyboardManager = new KeyboardManager();
        errorHandler = new ErrorHandler();
    }

    public void Start()
    {
        botClient.StartReceiving(HandleUpdateAsync, errorHandler.HandleErrorAsync);
        Console.WriteLine("Бот запущен...");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message != null)
        {
            await HandleMessage(botClient, update.Message);
        }
        else if (update.CallbackQuery != null)
        {
            await HandleCallbackQuery(botClient, update.CallbackQuery);
        }
    }

    private async Task HandleMessage(ITelegramBotClient botClient, Message message)
    {
        long chatId = message.Chat.Id;

        if (message.Text == "/start")
        {
            if (adminIds.Contains(chatId))
            {
                // Создаем обычную клавиатуру (ReplyKeyboardMarkup) с кнопками
                var menuKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Найти заявку", "Заявки", "Обновить категории" }
                })
                {
                    ResizeKeyboard = true, // Опционально, чтобы кнопки были адаптивными
                    OneTimeKeyboard = true // Опционально, чтобы клавиатура скрывалась после нажатия
                };

                // Отправляем сообщение с клавиатурой под полем ввода
                await botClient.SendTextMessageAsync(chatId, "Меню администратора", replyMarkup: menuKeyboard);
            }
            else
            {
                var inlineKeyboard = keyboardManager.CreateInlineKeyboard(new[]
                {
                new[] { InlineKeyboardButton.WithCallbackData("Перейти к верификации", "verify") }
            });

                Message sentMessage = await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в систему!", replyMarkup: inlineKeyboard);
                stepManager.SaveStep(chatId, "start", sentMessage.MessageId);
            }
        }
        else if (message.Text == "/menu" && adminIds.Contains(chatId))
        {
            // Создаем обычную клавиатуру (ReplyKeyboardMarkup) с кнопками
            var menuKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Найти заявку", "Заявки", "Обновить категории" }
            })
            {
                ResizeKeyboard = true, // Опционально, чтобы кнопки были адаптивными
                OneTimeKeyboard = true // Опционально, чтобы клавиатура скрывалась после нажатия
            };

            // Отправляем сообщение с клавиатурой под полем ввода
            await botClient.SendTextMessageAsync(chatId, "Меню администратора", replyMarkup: menuKeyboard);
        }
        else if (message.Text == "/find_application" && adminIds.Contains(chatId))
        {
            // Сохраняем шаг "waiting_for_application_id" для администратора
            stepManagerAdmin.SaveStep(chatId, "waiting_for_application_id", message.MessageId);

            // Отправляем сообщение с запросом ID заявки
            await botClient.SendTextMessageAsync(chatId, "Введите ID заявки для поиска:");
        }
        else if (message.Text == "Найти заявку" && adminIds.Contains(chatId))
        {
            // Сохраняем шаг "waiting_for_application_id" для администратора
            stepManagerAdmin.SaveStep(chatId, "waiting_for_application_id", message.MessageId);

            // Отправляем сообщение с запросом ID заявки
            await botClient.SendTextMessageAsync(chatId, "Введите ID заявки для поиска:");
        }
        else if (message.Text == "Заявки" && adminIds.Contains(chatId))
        {
            // Имитируем callback-запрос для отображения списка заявок
            var callbackQuery = new CallbackQuery
            {
                Data = "applications", // Начинаем с первой страницы (page = 0)
                Message = message // Передаем исходное сообщение для контекста
            };

            // Вызываем обработчик callback-запроса
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "/applications" && adminIds.Contains(chatId))
        {
            // Имитируем callback-запрос для отображения списка заявок
            var callbackQuery = new CallbackQuery
            {
                Data = "applications_0", // Начинаем с первой страницы (page = 0)
                Message = message // Передаем исходное сообщение для контекста
            };

            // Вызываем обработчик callback-запроса
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "/update_categories" && adminIds.Contains(chatId))
        {
            // Имитируем callback-запрос для обновления категорий
            var callbackQuery = new CallbackQuery
            {
                Data = "update_categories", // Данные для обновления категорий
                Message = message // Передаем исходное сообщение для контекста
            };

            // Вызываем обработчик callback-запроса
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "Обновить категории" && adminIds.Contains(chatId))
        {
            // Имитируем callback-запрос для обновления категорий
            var callbackQuery = new CallbackQuery
            {
                Data = "update_categories", // Данные для обновления категорий
                Message = message // Передаем исходное сообщение для контекста
            };

            // Вызываем обработчик callback-запроса
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (adminIds.Contains(chatId))
        {
            await stepManagerAdmin.HandleMessage(botClient, chatId, message);
        }
        else
        {
            await stepManager.HandleMessage(botClient, chatId, message);
        }
    }
    private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        long chatId = callbackQuery.Message.Chat.Id;
        string callbackData = callbackQuery.Data;

        if (adminIds.Contains(chatId))
        {
            // Обрабатываем кнопки только для админов
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else
        {
            await stepManager.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
    }

}

