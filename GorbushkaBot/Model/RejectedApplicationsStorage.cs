namespace GorbushkaBot.Model
{
    public static class RejectedApplicationsStorage
    {
        private static Dictionary<long, DateTime> rejectedApplications = new Dictionary<long, DateTime>();

        public static void AddRejected(long chatId)
        {
            rejectedApplications[chatId] = DateTime.UtcNow;
        }

        public static bool CanReapply(long chatId, out int minutesLeft)
        {
            if (rejectedApplications.TryGetValue(chatId, out DateTime rejectedTime))
            {
                TimeSpan elapsed = DateTime.UtcNow - rejectedTime;

                if (elapsed.TotalMinutes < 5)
                {
                    minutesLeft = 5 - (int)elapsed.TotalMinutes;
                    return false;
                }

                // Удаляем запись, если время истекло
                rejectedApplications.Remove(chatId);
            }

            minutesLeft = 0;
            return true;
        }
    }

}
