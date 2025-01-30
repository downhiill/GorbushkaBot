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
        private static readonly Dictionary<long, string> UserSteps = new Dictionary<long, string>(); // Хранение текущего шага пользователя

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
            else if (UserSteps.ContainsKey(chatId))
            {
                switch (UserSteps[chatId])
                {
                    case "fio":
                        UserSteps[chatId] = "passport_photo";
                        await AskForPassportPhoto(botClient, chatId);
                        break;
                    case "passport_photo":
                        UserSteps[chatId] = "role";
                        await AskForRoleSelection(botClient, chatId);
                        break;
                    case "role":
                        await FinalizeVerification(botClient, chatId);
                        break;
                }
            }
        }

        private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            long chatId = callbackQuery.Message.Chat.Id;
            int messageId = callbackQuery.Message.MessageId;

            if (callbackQuery.Data == "verify")
            {
                await StartVerification(botClient, chatId, messageId);
            }
            else if (callbackQuery.Data == "back")
            {
                if (UserSteps.TryGetValue(chatId, out var step))
                {
                    await HandleStepBack(botClient, chatId, messageId, step);
                }
            }
            else if (callbackQuery.Data == "seller" || callbackQuery.Data == "buyer" || callbackQuery.Data == "both")
            {
                await FinalizeVerification(botClient, chatId);
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private async Task StartVerification(ITelegramBotClient botClient, long chatId, int messageId)
        {
            UserSteps[chatId] = "fio";
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
            });

            await botClient.EditMessageTextAsync(chatId, messageId, "Шаг 1: Введите ваше ФИО.", replyMarkup: inlineKeyboard);
        }

        private async Task AskForPassportPhoto(ITelegramBotClient botClient, long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
            });

            await botClient.SendTextMessageAsync(chatId, "Шаг 2: Отправьте фото вашего паспорта.", replyMarkup: inlineKeyboard);
        }

        private async Task AskForRoleSelection(ITelegramBotClient botClient, long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Продавец", "seller"),
                    InlineKeyboardButton.WithCallbackData("Покупатель", "buyer"),
                    InlineKeyboardButton.WithCallbackData("Продавец и покупатель", "both")
                },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
            });

            await botClient.SendTextMessageAsync(chatId, "Шаг 3: Выберите вашу роль (Продавец, Покупатель или Продавец и Покупатель).", replyMarkup: inlineKeyboard);
        }

        private async Task FinalizeVerification(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SendTextMessageAsync(chatId, "✅ Верификация завершена успешно!");
            UserSteps.Remove(chatId);
        }

        private async Task HandleStepBack(ITelegramBotClient botClient, long chatId, int messageId, string step)
        {
            if (step == "passport_photo")
            {
                UserSteps[chatId] = "fio";
                await botClient.EditMessageTextAsync(chatId, messageId, "Шаг 1: Введите ваше ФИО.", replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
                }));
            }
            else if (step == "role")
            {
                UserSteps[chatId] = "passport_photo";
                await botClient.EditMessageTextAsync(chatId, messageId, "Шаг 2: Отправьте фото вашего паспорта.", replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
                }));
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
