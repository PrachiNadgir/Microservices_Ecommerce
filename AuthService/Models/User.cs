namespace AuthService.Models
{
    public class User
    {
        public string Role { get; set; } = "User"; // e.g., "Admin", "User"
        public int Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; } // (hashed in real apps)
    }
}