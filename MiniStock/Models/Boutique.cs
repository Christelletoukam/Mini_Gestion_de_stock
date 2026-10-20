namespace MiniStock.Models;

public sealed class Boutique
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nom { get; set; } = string.Empty;

    public string? Telephone { get; set; }

    public string? Adresse { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
}
