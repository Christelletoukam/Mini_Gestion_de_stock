using System.ComponentModel.DataAnnotations.Schema;

namespace MiniStock.Models;

public sealed class Produit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoutiqueId { get; set; }

    public Guid? CategorieId { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int PrixUnitaireFcfa { get; set; }

    public int QuantiteDisponible { get; set; }

    public int SeuilMinimum { get; set; }

    public bool EstActif { get; set; } = true;

    [NotMapped]
    public EtatStock NiveauStock =>
        QuantiteDisponible == 0
            ? EtatStock.Rupture
            : QuantiteDisponible <= SeuilMinimum
                ? EtatStock.Faible
                : EtatStock.Normal;
}
