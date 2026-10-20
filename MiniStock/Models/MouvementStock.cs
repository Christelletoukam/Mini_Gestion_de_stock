namespace MiniStock.Models;

public sealed class MouvementStock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoutiqueId { get; set; }

    public Guid ProduitId { get; set; }

    public Guid? UtilisateurId { get; set; }

    public TypeMouvementStock Type { get; set; }

    public int Variation { get; set; }

    public int QuantiteAvant { get; set; }

    public int QuantiteApres { get; set; }

    public string? Motif { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
}
