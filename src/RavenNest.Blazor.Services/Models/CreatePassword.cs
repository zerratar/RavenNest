using System.ComponentModel.DataAnnotations;

namespace RavenNest.Blazor.Services.Models
{
    public class CreatePassword
    {
        [Required]
        [StringLength(32, MinimumLength = 6)]
        public string Password { get; set; }
    }
}
