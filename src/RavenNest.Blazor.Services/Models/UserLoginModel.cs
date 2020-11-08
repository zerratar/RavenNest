using System.ComponentModel.DataAnnotations;

namespace RavenNest.Blazor.Services.Models
{
    public class UserLoginModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
