using GorbushkaBot.Controllers;
using GorbushkaBot.Service;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using GorbushkaBot.AppDbContext;

public class TelegramBotService
{
    private static readonly string BotToken = "8072900802:AAFoYeX1Ikv1_vwbGDJYfq_g64Kcaye5lGY";
    private static readonly string CredentialPath = "gorbushkarequest-56264ab50e01.json";
    private static readonly string SpreadsheetId = "1B25AYhgBmm3J-98peLHxjeOBeaVsbPfJcMoySV3EHAc";
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



    long[] adminIds = { 8018159474, 448145168, 7069858455 }; // Список ID администраторов

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
                await SetCommandsForAdmin(botClient, chatId); // Устанавливаем команды только для админов

                var menuKeyboard = GetAdminKeyboard();
                await botClient.SendTextMessageAsync(chatId, "Меню администратора", replyMarkup: menuKeyboard);
            }
            else
            {
                await botClient.DeleteMyCommandsAsync(); // Удаляем команды у обычных пользователей
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
            // Создаем клавиатуру для администраторов
            var menuKeyboard = new ReplyKeyboardMarkup(new[] {
            new KeyboardButton[] { "Найти заявку", "Заявки", "Обновить категории" }
        })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            // Отправляем сообщение с клавиатурой для администраторов
            await botClient.SendTextMessageAsync(chatId, "Меню администратора", replyMarkup: menuKeyboard);
        }
        else if (message.Text == "/find_application" && adminIds.Contains(chatId))
        {
            stepManagerAdmin.SaveStep(chatId, "waiting_for_application_id", message.MessageId);
            await botClient.SendTextMessageAsync(chatId, "Введите ID заявки для поиска:");
        }
        else if (message.Text == "Найти заявку" && adminIds.Contains(chatId))
        {
            stepManagerAdmin.SaveStep(chatId, "waiting_for_application_id", message.MessageId);
            await botClient.SendTextMessageAsync(chatId, "Введите ID заявки для поиска:");
        }
        else if (message.Text == "Заявки" && adminIds.Contains(chatId))
        {
            var callbackQuery = new CallbackQuery
            {
                Data = "applications_0",
                Message = message
            };
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "/applications" && adminIds.Contains(chatId))
        {
            var callbackQuery = new CallbackQuery
            {
                Data = "applications_0",
                Message = message
            };
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "/update_categories" && adminIds.Contains(chatId))
        {
            var callbackQuery = new CallbackQuery
            {
                Data = "update_categories",
                Message = message
            };
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "Обновить категории" && adminIds.Contains(chatId))
        {
            var callbackQuery = new CallbackQuery
            {
                Data = "update_categories",
                Message = message
            };
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "/black_list" && adminIds.Contains(chatId))
        {
            var callbackQuery = new CallbackQuery
            {
                Data = "black_list",
                Message = message
            };
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (message.Text == "Пройтись по блэк-листу" && adminIds.Contains(chatId))
        {
            var callbackQuery = new CallbackQuery
            {
                Data = "black_list",
                Message = message
            };
            await stepManagerAdmin.HandleCallbackQuery(botClient, chatId, callbackQuery);
        }
        else if (adminIds.Contains(chatId))
        {
            await stepManagerAdmin.HandleMessage(botClient, chatId, message);
        }
        else
        {
            // Для обычных пользователей
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

    private async Task SetCommandsForAdmin(ITelegramBotClient botClient, long userId)
    {
        var userCommands = new[]
        {
            new BotCommand { Command = "/help", Description = "Помощь" },
            new BotCommand { Command = "/info", Description = "Информация" }
        };

        if (adminIds.Contains(userId))
        {
            var adminCommands = new[]
            {
                new BotCommand { Command = "/find_application", Description = "Найти заявку" },
                new BotCommand { Command = "/applications", Description = "Список заявок" },
                new BotCommand { Command = "/update_categories", Description = "Обновить категории" },
                new BotCommand { Command = "/black_list", Description ="Пройтись по блэк-листу"}
            };

            // Устанавливаем команды для админа
            await botClient.SetMyCommandsAsync(adminCommands, new BotCommandScopeChat { ChatId = userId });
        }
        else
        {
            // Удаляем админские команды у пользователя
            await botClient.SetMyCommandsAsync(userCommands, new BotCommandScopeChat { ChatId = userId });
        }
    }


    private ReplyKeyboardMarkup GetAdminKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "Найти заявку", "Заявки", "Обновить категории", "Пройтись по блэк-листу" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }
    


}

