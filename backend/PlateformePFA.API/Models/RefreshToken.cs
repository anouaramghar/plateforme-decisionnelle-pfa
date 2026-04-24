using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public int UtilisateurId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreeLe { get; set; } = DateTime.UtcNow;

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    public Utilisateur Utilisateur { get; set; } = null!;
}
