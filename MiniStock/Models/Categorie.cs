namespace MiniStock.Models;

public sealed class Categorie
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoutiqueId { get; set; }

    public string Nom { get; set; } = string.Empty;
}
