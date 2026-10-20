using System.Collections.ObjectModel;

namespace MiniStock.ViewModels;

public sealed class MainPageViewModel
{
    public string Boutique { get; } = "Boutique Mboa · Douala";

    public string TotalArticles { get; } = "15";

    public string TotalEntrees { get; } = "15";

    public string TotalSorties { get; } = "10";

    public string NombreAlertes { get; } = "3";

    public string ValeurStock { get; } = "240 300 FCFA";

    public ObservableCollection<StockAlerteViewModel> ArticlesAlerte { get; } =
    [
        new("Sucre en poudre 1 kg", "0 unité", "Rupture", "#D9534F"),
        new("Huile de palme 1 L", "3 unités", "Faible", "#B7831B")
    ];
}

public sealed class StockAlerteViewModel
{
    public StockAlerteViewModel(string nom, string quantite, string etat, string couleur)
    {
        Nom = nom;
        Quantite = quantite;
        Etat = etat;
        Couleur = Color.FromArgb(couleur);
    }

    public string Nom { get; }

    public string Quantite { get; }

    public string Etat { get; }

    public Color Couleur { get; }
}
