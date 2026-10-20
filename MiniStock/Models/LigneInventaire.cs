namespace MiniStock.Models;

public sealed class LigneInventaire
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InventaireId { get; set; }

    public Guid ProduitId { get; set; }

    public int QuantiteTheorique { get; set; }

    public int? QuantitePhysique { get; set; }

    public int? Ecart => QuantitePhysique - QuantiteTheorique;
}
