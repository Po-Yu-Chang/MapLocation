namespace MapLocationApp.Models
{
    public class RememberedUser
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
        public DateTime LastRememberedAt { get; set; } = DateTime.Now;
    }
}