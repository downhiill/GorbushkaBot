using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class TelegramBotService
    {
        private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN")
            ?? throw new InvalidOperationException("BOT_TOKEN is not set");

        private readonly TelegramBotClient botClient;
        private readonly StepManager stepManager;
        private readonly KeyboardManager keyboardManager;
        private readonly ErrorHandler errorHandler;

        public TelegramBotService()
        {
            botClient = new TelegramBotClient(BotToken);
            stepManager = new StepManager(botClient);
            keyboardManager = new KeyboardManager();
            errorHandler = new ErrorHandler();
        }

        public void Start()
        {
            botClient.StartReceiving(HandleUpdateAsync, errorHandler.HandleErrorAsync);
            Console.WriteLine("Бот запущен...");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, System.Threading.CancellationToken cancellationToken)
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
}
