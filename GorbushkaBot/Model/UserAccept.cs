namespace GorbushkaBot.Model
{
    public class UserAccept
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public string UserNameTg { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string FacePhoto { get; set; }
        public string Fio { get; set; }
        public string PhoneNumber { get; set; }
        public string PassportNumber { get; set; }
        public string Role { get; set; }
        public string PassportIssueDate { get; set; }
        public string PassportIssueDateEnd { get; set; }
        public string RegistrationAddress { get; set; }
        public string PassportPhotos { get; set; }
        public string PavilionNumber { get; set; }
        public string RentalContract { get; set; }
        public string PavilionPhotos { get; set; }
        public string PropiskaPhoto { get; set; }
        public string FolderUrl { get; set; }
    }
}
