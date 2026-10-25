using Microsoft.Data.Sqlite;
using MiniStock.Services;

namespace MiniStock.Data;

public sealed class StockDatabase
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly string _databasePath = Path.Combine(FileSystem.AppDataDirectory, "ministock.db3");
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();

            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Boutiques (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Nom TEXT NOT NULL,
                    Telephone TEXT NOT NULL,
                    Adresse TEXT NULL,
                    CreeLe TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Utilisateurs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BoutiqueId TEXT NOT NULL,
                    Nom TEXT NOT NULL,
                    Identifiant TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    MotDePasseHash TEXT NOT NULL,
                    CreeLe TEXT NOT NULL,
                    FOREIGN KEY (BoutiqueId) REFERENCES Boutiques (Id)
                );

                CREATE TABLE IF NOT EXISTS Categories (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BoutiqueId TEXT NOT NULL,
                    Nom TEXT NOT NULL,
                    UNIQUE (BoutiqueId, Nom),
                    FOREIGN KEY (BoutiqueId) REFERENCES Boutiques (Id)
                );

                CREATE TABLE IF NOT EXISTS Produits (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BoutiqueId TEXT NOT NULL,
                    CategorieId TEXT NULL,
                    Nom TEXT NOT NULL,
                    Description TEXT NULL,
                    PrixUnitaireFcfa INTEGER NOT NULL CHECK (PrixUnitaireFcfa >= 0),
                    QuantiteDisponible INTEGER NOT NULL CHECK (QuantiteDisponible >= 0),
                    SeuilMinimum INTEGER NOT NULL CHECK (SeuilMinimum >= 0),
                    EstActif INTEGER NOT NULL DEFAULT 1,
                    ImagePath TEXT NULL,
                    FOREIGN KEY (BoutiqueId) REFERENCES Boutiques (Id),
                    FOREIGN KEY (CategorieId) REFERENCES Categories (Id)
                );

                CREATE TABLE IF NOT EXISTS MouvementsStock (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BoutiqueId TEXT NOT NULL,
                    ProduitId TEXT NOT NULL,
                    UtilisateurId TEXT NULL,
                    Type INTEGER NOT NULL,
                    Variation INTEGER NOT NULL,
                    QuantiteAvant INTEGER NOT NULL,
                    QuantiteApres INTEGER NOT NULL,
                    Motif TEXT NULL,
                    CreeLe TEXT NOT NULL,
                    FOREIGN KEY (BoutiqueId) REFERENCES Boutiques (Id),
                    FOREIGN KEY (ProduitId) REFERENCES Produits (Id),
                    FOREIGN KEY (UtilisateurId) REFERENCES Utilisateurs (Id)
                );

                CREATE TABLE IF NOT EXISTS Inventaires (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BoutiqueId TEXT NOT NULL,
                    UtilisateurId TEXT NULL,
                    CreeLe TEXT NOT NULL,
                    ValideLe TEXT NULL,
                    EstValide INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (BoutiqueId) REFERENCES Boutiques (Id),
                    FOREIGN KEY (UtilisateurId) REFERENCES Utilisateurs (Id)
                );

                CREATE TABLE IF NOT EXISTS LignesInventaire (
                    Id TEXT NOT NULL PRIMARY KEY,
                    InventaireId TEXT NOT NULL,
                    ProduitId TEXT NOT NULL,
                    QuantiteTheorique INTEGER NOT NULL,
                    QuantitePhysique INTEGER NULL,
                    FOREIGN KEY (InventaireId) REFERENCES Inventaires (Id),
                    FOREIGN KEY (ProduitId) REFERENCES Produits (Id)
                );

                CREATE INDEX IF NOT EXISTS IX_Produits_BoutiqueId ON Produits (BoutiqueId);
                CREATE INDEX IF NOT EXISTS IX_MouvementsStock_Boutique_CreeLe
                    ON MouvementsStock (BoutiqueId, CreeLe);
                """;
            await command.ExecuteNonQueryAsync();
            await EnsureSeedDataAsync(connection);
            await EnsureProductImagesAsync(connection);
            await EnsureMockHistoryAsync(connection);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    // Photos des articles de départ.
    // Chaque fichier doit exister dans Resources/Images (minuscules, sans espaces).
    // Supprime la ligne d'un article si tu n'as pas encore sa photo :
    // il affichera alors l'image de secours (box_placeholder.png).
    private static readonly (string Nom, string Image)[] ProductImages =
    {
        ("Riz parfumé Jasmine 5 kg", "riz_jasmine.jpg"),
        ("Huile de palme Mayor 1 L", "huile_palme.png"),
        ("Lait Peak non sucré 750 g", "lait_peak.jpg"),
        ("Riz Gino 5 kg", "riz_gino.png"),
        ("Sucre en poudre Tatie 750 g", "sucre_tatie.jpg"),
        ("Biscuits au chocolat", "biscuits_chocolat.png"),
        ("Eau minérale 1 L", "eau_minerale.jpg"),
        ("Jus d'orange", "jus_orange.jpg"),
        ("Savon liquide", "savon_liquide.jpg"),
        ("Shampooing 500 ml", "shampooing.jpg"),
        ("Pain de mie", "pain_de_mie.jpg"),
        ("Yaourt nature", "yaourt_nature.jpg"),
        ("Spaghetti Pasta", "spaghetti.jpg"),
        ("Café moulu 250 g", "cafe_moulu.jpg"),
        ("Farine de blé 1 kg", "farine_ble.jpg")
    };

    private static async Task EnsureProductImagesAsync(SqliteConnection connection)
    {
        var hasColumn = false;
        using (var infoCommand = connection.CreateCommand())
        {
            infoCommand.CommandText = "PRAGMA table_info(Produits);";
            using var reader = await infoCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "ImagePath", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Produits ADD COLUMN ImagePath TEXT NULL;";
            await alterCommand.ExecuteNonQueryAsync();
        }

        foreach (var (nom, image) in ProductImages)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                "UPDATE Produits SET ImagePath = $image WHERE Nom = $nom;";
            updateCommand.Parameters.AddWithValue("$image", image);
            updateCommand.Parameters.AddWithValue("$nom", nom);
            await updateCommand.ExecuteNonQueryAsync();
        }
    }

    private async Task EnsureSeedDataAsync(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Utilisateurs WHERE Identifiant = $email;";
        countCommand.Parameters.AddWithValue("$email", "demo@ministock.fr");
        var demoCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        if (demoCount > 0)
        {
            await UpgradeDemoSeedAsync(connection);
            return;
        }

        var boutiqueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var email = "demo@ministock.fr";
        var passwordHash = AuthenticationService.HashPassword("MiniStock123!");

        using var transaction = connection.BeginTransaction();

        using (var boutiqueCommand = connection.CreateCommand())
        {
            boutiqueCommand.Transaction = transaction;
            boutiqueCommand.CommandText =
                """
                INSERT INTO Boutiques (Id, Nom, Telephone, Adresse, CreeLe)
                VALUES ($id, $nom, $telephone, $adresse, $created);
                """;
            boutiqueCommand.Parameters.AddWithValue("$id", boutiqueId.ToString("D"));
            boutiqueCommand.Parameters.AddWithValue("$nom", "Boutique Mboa");
            boutiqueCommand.Parameters.AddWithValue("$telephone", "+221 77 000 00 00");
            boutiqueCommand.Parameters.AddWithValue("$adresse", "Douala, Cameroun");
            boutiqueCommand.Parameters.AddWithValue("$created", now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await boutiqueCommand.ExecuteNonQueryAsync();
        }

        using (var userCommand = connection.CreateCommand())
        {
            userCommand.Transaction = transaction;
            userCommand.CommandText =
                """
                INSERT INTO Utilisateurs (Id, BoutiqueId, Nom, Identifiant, MotDePasseHash, CreeLe)
                VALUES ($id, $boutiqueId, $nom, $email, $passwordHash, $created);
                """;
            userCommand.Parameters.AddWithValue("$id", userId.ToString("D"));
            userCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            userCommand.Parameters.AddWithValue("$nom", "Gérant");
            userCommand.Parameters.AddWithValue("$email", email);
            userCommand.Parameters.AddWithValue("$passwordHash", passwordHash);
            userCommand.Parameters.AddWithValue("$created", now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await userCommand.ExecuteNonQueryAsync();
        }

        var categoryIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Boissons"] = Guid.NewGuid().ToString("D"),
            ["Épicerie"] = Guid.NewGuid().ToString("D"),
            ["Hygiène"] = Guid.NewGuid().ToString("D"),
            ["Boulangerie"] = Guid.NewGuid().ToString("D")
        };

        foreach (var pair in categoryIds)
        {
            using var categoryCommand = connection.CreateCommand();
            categoryCommand.Transaction = transaction;
            categoryCommand.CommandText =
                """
                INSERT INTO Categories (Id, BoutiqueId, Nom)
                VALUES ($id, $boutiqueId, $nom);
                """;
            categoryCommand.Parameters.AddWithValue("$id", pair.Value);
            categoryCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            categoryCommand.Parameters.AddWithValue("$nom", pair.Key);
            await categoryCommand.ExecuteNonQueryAsync();
        }

        var sampleProducts = new[]
        {
            ("Riz parfumé Jasmine 5 kg", "Épicerie", 18500, 12, 5),
            ("Huile de palme Mayor 1 L", "Épicerie", 1500, 3, 5),
            ("Lait Peak non sucré 750 g", "Boulangerie", 650, 0, 8),
            ("Riz Gino 5 kg", "Épicerie", 16000, 24, 10),
            ("Sucre en poudre Tatie 750 g", "Épicerie", 1200, 0, 5),
            ("Biscuits au chocolat", "Épicerie", 450, 30, 10),
            ("Eau minérale 1 L", "Boissons", 350, 42, 12),
            ("Jus d'orange", "Boissons", 600, 18, 8),
            ("Savon liquide", "Hygiène", 950, 9, 6),
            ("Shampooing 500 ml", "Hygiène", 1200, 15, 8),
            ("Pain de mie", "Boulangerie", 550, 28, 12),
            ("Yaourt nature", "Boulangerie", 420, 22, 9),
            ("Pâtes italiennes", "Épicerie", 700, 16, 6),
            ("Café moulu 250 g", "Boissons", 2200, 14, 5),
            ("Farine de blé 1 kg", "Épicerie", 900, 20, 7)
        };

        foreach (var (name, category, unitPrice, quantity, minimum) in sampleProducts)
        {
            var productId = Guid.NewGuid();
            using var productCommand = connection.CreateCommand();
            productCommand.Transaction = transaction;
            productCommand.CommandText =
                """
                INSERT INTO Produits (Id, BoutiqueId, CategorieId, Nom, Description, PrixUnitaireFcfa, QuantiteDisponible, SeuilMinimum, EstActif)
                VALUES ($id, $boutiqueId, $categoryId, $nom, $description, $price, $quantity, $minimum, 1);
                """;
            productCommand.Parameters.AddWithValue("$id", productId.ToString("D"));
            productCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            productCommand.Parameters.AddWithValue("$categoryId", categoryIds[category]);
            productCommand.Parameters.AddWithValue("$nom", name);
            productCommand.Parameters.AddWithValue("$description", $"Produit de démonstration {name}.");
            productCommand.Parameters.AddWithValue("$price", unitPrice);
            productCommand.Parameters.AddWithValue("$quantity", quantity);
            productCommand.Parameters.AddWithValue("$minimum", minimum);
            await productCommand.ExecuteNonQueryAsync();

            var movementId = Guid.NewGuid();
            var startedAt = now.AddDays(-7);
            using var movementCommand = connection.CreateCommand();
            movementCommand.Transaction = transaction;
            movementCommand.CommandText =
                """
                INSERT INTO MouvementsStock (Id, BoutiqueId, ProduitId, UtilisateurId, Type, Variation, QuantiteAvant, QuantiteApres, Motif, CreeLe)
                VALUES ($id, $boutiqueId, $productId, $userId, $type, $variation, $before, $after, $motif, $created);
                """;
            movementCommand.Parameters.AddWithValue("$id", movementId.ToString("D"));
            movementCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            movementCommand.Parameters.AddWithValue("$productId", productId.ToString("D"));
            movementCommand.Parameters.AddWithValue("$userId", userId.ToString("D"));
            movementCommand.Parameters.AddWithValue("$type", 0);
            movementCommand.Parameters.AddWithValue("$variation", quantity);
            movementCommand.Parameters.AddWithValue("$before", 0);
            movementCommand.Parameters.AddWithValue("$after", quantity);
            movementCommand.Parameters.AddWithValue("$motif", "Stock initial");
            movementCommand.Parameters.AddWithValue("$created", startedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await movementCommand.ExecuteNonQueryAsync();
        }

        using (var inventoryCommand = connection.CreateCommand())
        {
            inventoryCommand.Transaction = transaction;
            inventoryCommand.CommandText =
                """
                INSERT INTO Inventaires (Id, BoutiqueId, UtilisateurId, CreeLe, ValideLe, EstValide)
                VALUES ($id, $boutiqueId, $userId, $created, $validated, 1);
                """;
            inventoryCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            inventoryCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            inventoryCommand.Parameters.AddWithValue("$userId", userId.ToString("D"));
            inventoryCommand.Parameters.AddWithValue("$created", now.AddDays(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            inventoryCommand.Parameters.AddWithValue("$validated", now.AddDays(-1).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await inventoryCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task UpgradeDemoSeedAsync(SqliteConnection connection)
    {
        using var findDemo = connection.CreateCommand();
        findDemo.CommandText = "SELECT Id, BoutiqueId FROM Utilisateurs WHERE Identifiant = $email;";
        findDemo.Parameters.AddWithValue("$email", "demo@ministock.fr");
        using var reader = await findDemo.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return;
        }

        var userId = reader.GetString(0);
        var boutiqueId = reader.GetString(1);
        await reader.CloseAsync();

        using var checkCatalog = connection.CreateCommand();
        checkCatalog.CommandText =
            """
            SELECT
                COUNT(*),
                SUM(CASE WHEN Description LIKE 'Produit de démonstration %' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Nom IN (
                    'Eau minérale 1L', 'Jus d''orange', 'Biscuits chocolat', 'Savon liquide',
                    'Shampooing 500ml', 'Pain de mie', 'Yaourt nature', 'Pâtes italiennes'
                ) THEN 1 ELSE 0 END)
            FROM Produits
            WHERE BoutiqueId = $boutiqueId;
            """;
        checkCatalog.Parameters.AddWithValue("$boutiqueId", boutiqueId);
        using var catalogReader = await checkCatalog.ExecuteReaderAsync();
        if (!await catalogReader.ReadAsync() ||
            Convert.ToInt32(catalogReader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) != 8 ||
            Convert.ToInt32(catalogReader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture) != 8 ||
            Convert.ToInt32(catalogReader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture) != 8)
        {
            return;
        }
        await catalogReader.CloseAsync();

        var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var categoryCommand = connection.CreateCommand())
        {
            categoryCommand.CommandText = "SELECT Id, Nom FROM Categories WHERE BoutiqueId = $boutiqueId;";
            categoryCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId);
            using var categoryReader = await categoryCommand.ExecuteReaderAsync();
            while (await categoryReader.ReadAsync())
            {
                categories[categoryReader.GetString(1)] = categoryReader.GetString(0);
            }
        }

        var additions = new[]
        {
            ("Riz parfumé Jasmine 5 kg", "Épicerie", 18500, 12, 5),
            ("Huile de palme Mayor 1 L", "Épicerie", 1500, 3, 5),
            ("Lait Peak non sucré 750 g", "Boulangerie", 650, 0, 8),
            ("Riz Gino 5 kg", "Épicerie", 16000, 24, 10),
            ("Sucre en poudre Tatie 750 g", "Épicerie", 1200, 0, 5),
            ("Café moulu 250 g", "Boissons", 2200, 14, 5),
            ("Farine de blé 1 kg", "Épicerie", 900, 20, 7)
        };
        var now = DateTimeOffset.UtcNow;
        if (additions.Any(product => !categories.ContainsKey(product.Item2)))
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var (name, category, price, quantity, minimum) in additions)
        {
            using var productCommand = connection.CreateCommand();
            productCommand.Transaction = transaction;
            productCommand.CommandText =
                """
                INSERT INTO Produits (Id, BoutiqueId, CategorieId, Nom, Description, PrixUnitaireFcfa, QuantiteDisponible, SeuilMinimum, EstActif)
                VALUES ($id, $boutiqueId, $categoryId, $name, $description, $price, $quantity, $minimum, 1);
                """;
            var productId = Guid.NewGuid();
            productCommand.Parameters.AddWithValue("$id", productId.ToString("D"));
            productCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId);
            productCommand.Parameters.AddWithValue("$categoryId", categories[category]);
            productCommand.Parameters.AddWithValue("$name", name);
            productCommand.Parameters.AddWithValue("$description", $"Produit de démonstration {name}.");
            productCommand.Parameters.AddWithValue("$price", price);
            productCommand.Parameters.AddWithValue("$quantity", quantity);
            productCommand.Parameters.AddWithValue("$minimum", minimum);
            await productCommand.ExecuteNonQueryAsync();

            using var movementCommand = connection.CreateCommand();
            movementCommand.Transaction = transaction;
            movementCommand.CommandText =
                """
                INSERT INTO MouvementsStock (Id, BoutiqueId, ProduitId, UtilisateurId, Type, Variation, QuantiteAvant, QuantiteApres, Motif, CreeLe)
                VALUES ($id, $boutiqueId, $productId, $userId, 0, $quantity, 0, $quantity, 'Stock initial', $created);
                """;
            movementCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            movementCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId);
            movementCommand.Parameters.AddWithValue("$productId", productId.ToString("D"));
            movementCommand.Parameters.AddWithValue("$userId", userId);
            movementCommand.Parameters.AddWithValue("$quantity", quantity);
            movementCommand.Parameters.AddWithValue("$created", now.AddDays(-7).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await movementCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task EnsureMockHistoryAsync(SqliteConnection connection)
    {
        string boutiqueId;
        string managerId;
        using (var findDemo = connection.CreateCommand())
        {
            findDemo.CommandText = "SELECT Id, BoutiqueId FROM Utilisateurs WHERE Identifiant = $email;";
            findDemo.Parameters.AddWithValue("$email", "demo@ministock.fr");
            using var reader = await findDemo.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return;
            }

            managerId = reader.GetString(0);
            boutiqueId = reader.GetString(1);
        }

        using (var checkHistory = connection.CreateCommand())
        {
            checkHistory.CommandText = "SELECT COUNT(*) FROM MouvementsStock WHERE BoutiqueId = $boutiqueId AND Type <> 0;";
            checkHistory.Parameters.AddWithValue("$boutiqueId", boutiqueId);
            if (Convert.ToInt32(await checkHistory.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        var products = new List<(string Id, int Quantity, int Minimum)>();
        using (var productCommand = connection.CreateCommand())
        {
            productCommand.CommandText =
                "SELECT Id, QuantiteDisponible, SeuilMinimum FROM Produits WHERE BoutiqueId = $boutiqueId AND EstActif = 1 ORDER BY Nom;";
            productCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId);
            using var reader = await productCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add((reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
            }
        }

        if (products.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        using var transaction = connection.BeginTransaction();

        var cashierId = Guid.NewGuid().ToString("D");
        using (var cashierCommand = connection.CreateCommand())
        {
            cashierCommand.Transaction = transaction;
            cashierCommand.CommandText =
                """
                INSERT OR IGNORE INTO Utilisateurs (Id, BoutiqueId, Nom, Identifiant, MotDePasseHash, CreeLe)
                VALUES ($id, $boutiqueId, $nom, $email, $passwordHash, $created);
                """;
            cashierCommand.Parameters.AddWithValue("$id", cashierId);
            cashierCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId);
            cashierCommand.Parameters.AddWithValue("$nom", "Caissière Awa");
            cashierCommand.Parameters.AddWithValue("$email", "caisse@ministock.fr");
            cashierCommand.Parameters.AddWithValue("$passwordHash", AuthenticationService.HashPassword("MiniStock123!"));
            cashierCommand.Parameters.AddWithValue("$created", now.AddDays(-7).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            if (await cashierCommand.ExecuteNonQueryAsync() == 0)
            {
                cashierId = managerId;
            }
        }

        var exitReasons = new[] { "Vente comptoir", "Vente client fidèle", "Commande restaurant voisin", "Vente au détail" };
        var entryReasons = new[] { "Réapprovisionnement fournisseur", "Livraison grossiste", "Achat au marché central" };
        var random = new Random(2026);

        foreach (var (productId, finalQuantity, minimum) in products)
        {
            // Historique fictif après le stock initial (J-7) qui revient exactement à la quantité actuelle.
            var current = finalQuantity;
            var steps = new List<(int Type, int Variation, string Motif)>();
            var stepCount = random.Next(3, 7);
            for (var i = 0; i < stepCount; i++)
            {
                var roll = random.NextDouble();
                if (current > 0 && roll < 0.6)
                {
                    var sold = random.Next(1, current / 3 + 2);
                    steps.Add((1, -sold, exitReasons[random.Next(exitReasons.Length)]));
                    current -= sold;
                }
                else if (current > 0 && roll < 0.7)
                {
                    steps.Add((2, -1, "Écart constaté : article abîmé"));
                    current -= 1;
                }
                else
                {
                    var received = random.Next(Math.Max(1, minimum), Math.Max(2, minimum * 2) + 1);
                    steps.Add((0, received, entryReasons[random.Next(entryReasons.Length)]));
                    current += received;
                }
            }

            var gap = finalQuantity - current;
            if (gap > 0)
            {
                steps.Add((0, gap, entryReasons[random.Next(entryReasons.Length)]));
            }
            else if (gap < 0)
            {
                steps.Add((1, gap, finalQuantity == 0 ? "Vente du dernier stock" : exitReasons[random.Next(exitReasons.Length)]));
            }

            var offsets = Enumerable.Range(0, steps.Count)
                .Select(_ => TimeSpan.FromMinutes(random.Next(60, 6 * 24 * 60)))
                .OrderByDescending(offset => offset)
                .ToList();

            var quantity = finalQuantity;
            for (var i = 0; i < steps.Count; i++)
            {
                var (type, variation, motif) = steps[i];
                using var movementCommand = connection.CreateCommand();
                movementCommand.Transaction = transaction;
                movementCommand.CommandText =
                    """
                    INSERT INTO MouvementsStock (Id, BoutiqueId, ProduitId, UtilisateurId, Type, Variation, QuantiteAvant, QuantiteApres, Motif, CreeLe)
                    VALUES ($id, $boutiqueId, $productId, $userId, $type, $variation, $before, $after, $motif, $created);
                    """;
                movementCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
                movementCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId);
                movementCommand.Parameters.AddWithValue("$productId", productId);
                movementCommand.Parameters.AddWithValue("$userId", type == 1 ? cashierId : managerId);
                movementCommand.Parameters.AddWithValue("$type", type);
                movementCommand.Parameters.AddWithValue("$variation", variation);
                movementCommand.Parameters.AddWithValue("$before", quantity);
                movementCommand.Parameters.AddWithValue("$after", quantity + variation);
                movementCommand.Parameters.AddWithValue("$motif", motif);
                movementCommand.Parameters.AddWithValue("$created", now.Subtract(offsets[i]).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                await movementCommand.ExecuteNonQueryAsync();
                quantity += variation;
            }
        }

        await transaction.CommitAsync();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }
}