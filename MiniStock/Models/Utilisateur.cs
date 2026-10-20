namespace MiniStock.Models;

public sealed class Utilisateur
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoutiqueId { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string Identifiant { get; set; } = string.Empty;

    public string MotDePasseHash { get; set; } = string.Empty;

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
}
