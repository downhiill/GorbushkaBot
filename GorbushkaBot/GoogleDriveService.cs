using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Telegram.Bot;

namespace GorbushkaBot
{
    // Сервис для работы с Google Drive
    public class GoogleDriveService
    {
        private readonly DriveService _service;
        private readonly string _parentFolderId;
        private readonly string _botToken;

        public GoogleDriveService()
        {
            _parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_FOLDER_ID");
            _botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
            var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_PATH");

            var credential = GoogleCredential.FromFile(credentialPath)
                .CreateScoped(DriveService.Scope.Drive);

            _service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "GorbushkaBot"
            });
        }

        public async Task<Dictionary<string, string>> CreateUserFolderAsync(long userId)
        {
            var folders = new Dictionary<string, string>();

            // Создаем корневую папку
            var rootFolder = await CreateFolderAsync($"User_{userId}", _parentFolderId);
            folders.Add("root", rootFolder.Id);

            // Создаем подпапки и сохраняем их ID
            folders.Add("face", (await CreateFolderAsync("face", rootFolder.Id)).Id);
            folders.Add("passport", (await CreateFolderAsync("passport", rootFolder.Id)).Id);
            folders.Add("pavilion", (await CreateFolderAsync("pavilion", rootFolder.Id)).Id);

            return folders;
        }
        private async Task<Google.Apis.Drive.v3.Data.File> CreateFolderAsync(string name, string parentId)
        {
            var folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = name,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { parentId }
            };

            var request = _service.Files.Create(folderMetadata);
            request.Fields = "id";
            return await request.ExecuteAsync();
        }

        public async Task UploadPhotosAsync(ITelegramBotClient botClient, string folderId, IEnumerable<string> fileIds)
        {
            using var httpClient = new HttpClient();

            foreach (var fileId in fileIds)
            {
                try
                {
                    // Получаем информацию о файле
                    var file = await botClient.GetFileAsync(fileId);

                    // Формируем URL для скачивания
                    var fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{file.FilePath}";

                    // Скачиваем файл
                    using var response = await httpClient.GetAsync(fileUrl);
                    using var stream = await response.Content.ReadAsStreamAsync();

                    // Создаем метаданные для Google Drive
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File
                    {
                        Name = $"{DateTime.Now:yyyyMMddHHmmss}_{fileId}.jpg",
                        Parents = new List<string> { folderId }
                    };

                    // Загружаем в Google Drive
                    var request = _service.Files.Create(fileMetadata, stream, "image/jpeg");
                    request.Fields = "id";
                    await request.UploadAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки файла {fileId}: {ex.Message}");
                }
            }
        }


        public async Task UploadPhotosAsync(ITelegramBotClient botClient, string folderId, IEnumerable<string> fileIds, string bottoken)
        {
            using var httpClient = new HttpClient();

            foreach (var fileId in fileIds)
            {
                var file = await botClient.GetFileAsync(fileId);
                var fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{file.FilePath}";

                using var response = await httpClient.GetAsync(fileUrl);
                using var stream = await response.Content.ReadAsStreamAsync();

                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = $"{fileId}.jpg",
                    Parents = new List<string> { folderId }
                };

                var request = _service.Files.Create(fileMetadata, stream, "image/jpeg");
                request.Fields = "id";
                await request.UploadAsync();
            }
        }
    }
}

