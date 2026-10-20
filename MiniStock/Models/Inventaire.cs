namespace MiniStock.Models;

public sealed class Inventaire
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoutiqueId { get; set; }

    public Guid? UtilisateurId { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ValideLe { get; set; }

    public bool EstValide { get; set; }
}
