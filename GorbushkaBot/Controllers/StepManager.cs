using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class StepManager
    {
        private static readonly Dictionary<long, (string step, int messageId)> UserSteps = new();
        private readonly TelegramBotClient botClient;

        public StepManager(TelegramBotClient botClient)
        {
            this.botClient = botClient;
        }

        public void SaveStep(long chatId, string step, int messageId)
        {
            UserSteps[chatId] = (step, messageId);
        }

        public async Task HandleMessage(ITelegramBotClient botClient, long chatId, Message message)
        {
            if (!UserSteps.ContainsKey(chatId)) return;

            var (step, messageId) = UserSteps[chatId];

            if (step == "fio")
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(message.Text, "^[А-Яа-яA-Za-z ]+$"))
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: ФИО должно содержать только буквы и пробелы.");
                    return;
                }
                await UpdateVerificationStep(botClient, chatId, messageId, "passport_number", "Введите номер вашего паспорта:", true);
            }
            else if (step == "passport_number")
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(message.Text, @"^\d{4} \d{6}$"))
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: Введите корректный номер паспорта (формат: 0000 000000).");
                    return;
                }
                await UpdateVerificationStep(botClient, chatId, messageId, "passport_issue_date", "Введите дату выдачи паспорта (в формате ДД.ММ.ГГГГ):", true);
            }
            else if (step == "passport_issue_date")
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(message.Text, @"^\d{2}\.\d{2}\.\d{4}$"))
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: Введите корректную дату в формате ДД.ММ.ГГГГ.");
                    return;
                }
                await UpdateVerificationStep(botClient, chatId, messageId, "passport", "Отправьте фото страниц своего паспорта, на которых находятся:\n- ФИО\n- Номер\n- Дата выдачи\n- Прописка\n\nВы можете отправить несколько фото.", true);
            }
            else if (step == "passport")
            {
                if (message.Photo == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: Отправьте именно фото, а не текст или документ.");
                    return;
                }
                await botClient.SendTextMessageAsync(chatId, "Фото получено! Если у вас есть ещё страницы, отправьте их. Если все страницы отправлены, нажмите кнопку 'Далее'.", replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Далее", "next_after_passport") }
                }));
            }
        }

        public async Task HandleCallbackQuery(ITelegramBotClient botClient, long chatId, CallbackQuery callbackQuery)
        {
            string data = callbackQuery.Data;

            switch (data)
            {
                case "verify":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "fio", "Введите ваше ФИО:", false);
                    break;
                case "next_after_passport":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        }));
                    break;
            }
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private async Task UpdateVerificationStep(ITelegramBotClient botClient, long chatId, int messageId, string nextStep, string messageText, bool isTextInput, InlineKeyboardMarkup? keyboard = null)
        {
            if (keyboard == null)
            {
                keyboard = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") } });
            }
            await botClient.EditMessageTextAsync(chatId, messageId, messageText, replyMarkup: keyboard);
            SaveStep(chatId, nextStep, messageId);
        }
    }
}