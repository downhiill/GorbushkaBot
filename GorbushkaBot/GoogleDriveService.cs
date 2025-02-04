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
            var rootFolderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = $"User_{userId}",
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { _parentFolderId }
            };

            var rootRequest = _service.Files.Create(rootFolderMetadata);
            rootRequest.Fields = "id, webViewLink";
            var rootFolder = await rootRequest.ExecuteAsync();

            var folders = new Dictionary<string, string>
            {
                ["root"] = rootFolder.WebViewLink // Ссылка на корневую папку
            };

            string[] subFolderNames = { "face", "passport", "pavilion" };

            foreach (var folderName in subFolderNames)
            {
                var subFolderMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { rootFolder.Id }
                };

                var subFolderRequest = _service.Files.Create(subFolderMetadata);
                subFolderRequest.Fields = "id, webViewLink";
                var subFolder = await subFolderRequest.ExecuteAsync();

                folders[folderName] = subFolder.WebViewLink; // Сохраняем ссылку, а не ID
            }

            return folders;
        }

        public async Task UploadPhotosAsync(ITelegramBotClient botClient, string folderId, IEnumerable<string> fileIds)
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

