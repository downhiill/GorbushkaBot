using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class KeyboardManager
    {
        public InlineKeyboardMarkup CreateInlineKeyboard(InlineKeyboardButton[][] buttons)
        {
            return new InlineKeyboardMarkup(buttons);
        }
    }
}
