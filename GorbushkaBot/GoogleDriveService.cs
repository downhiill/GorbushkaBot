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
        private readonly string ParentFolderId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_FOLDER_ID");

        public GoogleDriveService(string credentialPath)
        {
            var credential = GoogleCredential.FromFile(credentialPath)
                .CreateScoped(DriveService.Scope.Drive);

            _service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GorbushkaBot"
            });
        }

        public async Task<string> CreateUserFolderAsync(long userId)
        {
            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = $"User_{userId}",
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { ParentFolderId }
            };

            var request = _service.Files.Create(folderMetadata);
            request.Fields = "id, webViewLink";
            var folder = await request.ExecuteAsync();
            return folder.WebViewLink;
        }

        public async Task UploadPhotosAsync(ITelegramBotClient botClient, string folderId, IEnumerable<string> fileIds, string bottoken)
        {
            using var httpClient = new HttpClient();

            foreach (var fileId in fileIds)
            {
                var file = await botClient.GetFileAsync(fileId);
                var fileUrl = $"https://api.telegram.org/file/bot{bottoken}/{file.FilePath}";

                using var response = await httpClient.GetAsync(fileUrl);
                using var stream = await response.Content.ReadAsStreamAsync();

                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
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
