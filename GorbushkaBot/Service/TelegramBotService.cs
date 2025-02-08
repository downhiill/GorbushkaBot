using GorbushkaBot.Controllers;
using GorbushkaBot.Service;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;

public class TelegramBotService
{
    private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN")
        ?? throw new InvalidOperationException("BOT_TOKEN is not set");

    private static readonly string CredentialPath = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_PATH")
        ?? throw new InvalidOperationException("GOOGLE_CREDENTIALS_PATH is not set");

    private static readonly string SpreadsheetId = Environment.GetEnvironmentVariable("GOOGLE_SHEET_ID")
        ?? throw new InvalidOperationException("GOOGLE_SHEET_ID is not set");

    private readonly TelegramBotClient botClient;
    private readonly StepManager stepManager;
    private readonly KeyboardManager keyboardManager;
    private readonly ErrorHandler errorHandler;
    private readonly GoogleSheetsService googleSheetsService;
    private readonly GoogleDriveService googleDriveService;
    private readonly UserApplicationService userApplicationService;

    // Добавляем параметр userApplicationService
    public TelegramBotService(UserApplicationService userApplicationService)
    {
        botClient = new TelegramBotClient(BotToken);
        googleSheetsService = new GoogleSheetsService(CredentialPath, SpreadsheetId, userApplicationService); // Передаем userApplicationService
        googleDriveService = new GoogleDriveService();
        stepManager = new StepManager(botClient, googleSheetsService, googleDriveService, userApplicationService);
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
            var inlineKeyboard = keyboardManager.CreateInlineKeyboard(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Перейти к верификации", "verify") }
            });

            Message sentMessage = await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в систему!", replyMarkup: inlineKeyboard);
            stepManager.SaveStep(chatId, "start", sentMessage.MessageId);
        }
        else
        {
            await stepManager.HandleMessage(botClient, chatId, message);
        }
    }

    private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        long chatId = callbackQuery.Message.Chat.Id;
        await stepManager.HandleCallbackQuery(botClient, chatId, callbackQuery);
    }
}
