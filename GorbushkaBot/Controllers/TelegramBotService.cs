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
        private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? throw new InvalidOperationException("BOT_TOKEN is not set");
        private readonly TelegramBotClient botClient;
        private static readonly Dictionary<long, string> UserSteps = new Dictionary<long, string>();

        public TelegramBotService()
        {
            botClient = new TelegramBotClient(BotToken);
        }

        public void Start()
        {
            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            Console.WriteLine("Бот запущен...");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, System.Threading.CancellationToken cancellationToken)
        {
            if (update.Message is { } message)
            {
                await HandleMessage(botClient, message);
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQuery(botClient, callbackQuery);
            }
        }

        private async Task HandleMessage(ITelegramBotClient botClient, Message message)
        {
            long chatId = message.Chat.Id;

            if (message.Text == "/start")
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Перейти к верификации", "verify") }
                });

                await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в систему!", replyMarkup: inlineKeyboard);
            }
            else if (UserSteps.ContainsKey(chatId) && UserSteps[chatId] == "fio")
            {
                // Пользователь ввел ФИО, можно сохранить
                UserSteps.Remove(chatId);
                await botClient.SendTextMessageAsync(chatId, $"Ваше ФИО: {message.Text}\n✅ Верификация завершена!");
            }
        }

        private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            long chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data == "verify")
            {
                UserSteps[chatId] = "fio";
                await botClient.SendTextMessageAsync(chatId, "Введите ваше ФИО:");
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}