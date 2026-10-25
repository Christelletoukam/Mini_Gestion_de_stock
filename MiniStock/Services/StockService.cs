using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;
using MiniStock.Data;
using MiniStock.Models;

namespace MiniStock.Services;

public sealed class StockService(StockDatabase database) : IStockService
{
    public async Task<IReadOnlyList<StockProduct>> GetProductsAsync()
    {
        var boutiqueId = await GetBoutiqueIdAsync();
        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.Id, p.Nom, p.QuantiteDisponible, p.SeuilMinimum, p.PrixUnitaireFcfa, c.Nom, p.ImagePath
            FROM Produits p
            LEFT JOIN Categories c ON c.Id = p.CategorieId
            WHERE p.BoutiqueId = $boutiqueId AND p.EstActif = 1
            ORDER BY p.Nom COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));

        var products = new List<StockProduct>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new StockProduct(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return products;
    }

    public async Task<IReadOnlyList<StockProduct>> GetAlertsAsync() =>
        (await GetProductsAsync())
        .Where(product => product.Quantity <= product.MinimumQuantity)
        .ToArray();

    public async Task AddProductAsync(
        string name,
        string? category,
        int unitPrice,
        int quantity,
        int minimumQuantity)
    {
        if (string.IsNullOrWhiteSpace(name) || unitPrice < 0 || quantity < 0 || minimumQuantity < 0)
        {
            throw new ArgumentException("Les informations de l’article sont invalides.");
        }

        var (userId, boutiqueId) = await GetAccountIdsAsync();
        using var connection = database.CreateConnection();
        using var transaction = connection.BeginTransaction();
        string? categoryId = null;

        if (!string.IsNullOrWhiteSpace(category))
        {
            using var categoryCommand = connection.CreateCommand();
            categoryCommand.Transaction = transaction;
            categoryCommand.CommandText =
                """
                INSERT OR IGNORE INTO Categories (Id, BoutiqueId, Nom)
                VALUES ($id, $boutiqueId, $nom);
                """;
            categoryCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            categoryCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            categoryCommand.Parameters.AddWithValue("$nom", category.Trim());
            await categoryCommand.ExecuteNonQueryAsync();

            using var findCategory = connection.CreateCommand();
            findCategory.Transaction = transaction;
            findCategory.CommandText =
                "SELECT Id FROM Categories WHERE BoutiqueId = $boutiqueId AND Nom = $nom;";
            findCategory.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            findCategory.Parameters.AddWithValue("$nom", category.Trim());
            categoryId = (string?)await findCategory.ExecuteScalarAsync();
        }

        var productId = Guid.NewGuid();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO Produits
                    (Id, BoutiqueId, CategorieId, Nom, PrixUnitaireFcfa, QuantiteDisponible, SeuilMinimum)
                VALUES
                    ($id, $boutiqueId, $categoryId, $nom, $price, $quantity, $minimum);
                """;
            command.Parameters.AddWithValue("$id", productId.ToString("D"));
            command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            command.Parameters.AddWithValue("$categoryId", (object?)categoryId ?? DBNull.Value);
            command.Parameters.AddWithValue("$nom", name.Trim());
            command.Parameters.AddWithValue("$price", unitPrice);
            command.Parameters.AddWithValue("$quantity", quantity);
            command.Parameters.AddWithValue("$minimum", minimumQuantity);
            await command.ExecuteNonQueryAsync();
        }

        if (quantity > 0)
        {
            await InsertMovementAsync(
                connection,
                transaction,
                boutiqueId,
                productId,
                userId,
                TypeMouvementStock.Entree,
                quantity,
                0,
                quantity,
                "Stock initial");
        }

        await transaction.CommitAsync();
    }

    public async Task UpdateProductPricingAsync(Guid productId, int unitPrice, int minimumQuantity)
    {
        if (unitPrice < 0 || minimumQuantity < 0)
        {
            throw new ArgumentException("Le prix et le seuil d’alerte doivent être positifs ou nuls.");
        }

        var boutiqueId = await GetBoutiqueIdAsync();
        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Produits
            SET PrixUnitaireFcfa = $price, SeuilMinimum = $minimum
            WHERE Id = $id AND BoutiqueId = $boutiqueId AND EstActif = 1;
            """;
        command.Parameters.AddWithValue("$price", unitPrice);
        command.Parameters.AddWithValue("$minimum", minimumQuantity);
        command.Parameters.AddWithValue("$id", productId.ToString("D"));
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new InvalidOperationException("L’article sélectionné n’existe plus.");
        }
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync()
    {
        var boutiqueId = await GetBoutiqueIdAsync();
        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT m.Id, p.Nom, m.Type, m.Variation, m.QuantiteAvant, m.QuantiteApres, m.Motif, m.CreeLe
            FROM MouvementsStock m
            INNER JOIN Produits p ON p.Id = m.ProduitId
            WHERE m.BoutiqueId = $boutiqueId
            ORDER BY m.CreeLe DESC
            LIMIT 100;
            """;
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));

        var movements = new List<StockMovement>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            movements.Add(new StockMovement(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                (TypeMouvementStock)reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture)));
        }

        return movements;
    }

    public async Task<IReadOnlyList<StockMovement>> GetProductMovementsAsync(Guid productId)
    {
        var boutiqueId = await GetBoutiqueIdAsync();
        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT m.Id, p.Nom, m.Type, m.Variation, m.QuantiteAvant, m.QuantiteApres, m.Motif, m.CreeLe
            FROM MouvementsStock m
            INNER JOIN Produits p ON p.Id = m.ProduitId
            WHERE m.BoutiqueId = $boutiqueId AND m.ProduitId = $productId
            ORDER BY m.CreeLe DESC;
            """;
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
        command.Parameters.AddWithValue("$productId", productId.ToString("D"));

        var movements = new List<StockMovement>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            movements.Add(new StockMovement(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                (TypeMouvementStock)reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture)));
        }

        return movements;
    }

    public async Task RegisterMovementAsync(Guid productId, TypeMouvementStock type, int quantity, string? reason)
    {
        if (quantity <= 0 || !Enum.IsDefined(type) || type == TypeMouvementStock.CorrectionInventaire)
        {
            throw new ArgumentException("La quantité doit être supérieure à zéro.");
        }

        var (userId, boutiqueId) = await GetAccountIdsAsync();
        using var connection = database.CreateConnection();
        using var transaction = connection.BeginTransaction();

        int before;
        using (var findProduct = connection.CreateCommand())
        {
            findProduct.Transaction = transaction;
            findProduct.CommandText =
                "SELECT QuantiteDisponible FROM Produits WHERE Id = $productId AND BoutiqueId = $boutiqueId AND EstActif = 1;";
            findProduct.Parameters.AddWithValue("$productId", productId.ToString("D"));
            findProduct.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            var result = await findProduct.ExecuteScalarAsync();
            if (result is null)
            {
                throw new InvalidOperationException("L’article sélectionné n’existe plus.");
            }

            before = Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        if (type == TypeMouvementStock.Sortie && quantity > before)
        {
            throw new InvalidOperationException("La sortie dépasse la quantité disponible.");
        }

        if (type == TypeMouvementStock.Entree && quantity > int.MaxValue - before)
        {
            throw new InvalidOperationException("La quantité dépasse la capacité maximale du stock.");
        }

        var delta = type == TypeMouvementStock.Entree ? quantity : -quantity;
        var after = before + delta;

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Produits SET QuantiteDisponible = $quantity WHERE Id = $id;";
            update.Parameters.AddWithValue("$quantity", after);
            update.Parameters.AddWithValue("$id", productId.ToString("D"));
            await update.ExecuteNonQueryAsync();
        }

        await InsertMovementAsync(connection, transaction, boutiqueId, productId, userId, type, delta, before, after, reason);
        await transaction.CommitAsync();
    }

    public async Task SaveInventoryAsync(IReadOnlyDictionary<Guid, int> physicalQuantities)
    {
        if (physicalQuantities.Count == 0)
        {
            throw new ArgumentException("L’inventaire doit contenir au moins un article.");
        }

        if (physicalQuantities.Any(item => item.Value < 0))
        {
            throw new ArgumentException("Les quantités physiques ne peuvent pas être négatives.");
        }

        var (userId, boutiqueId) = await GetAccountIdsAsync();
        using var connection = database.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var inventoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var insertInventory = connection.CreateCommand())
        {
            insertInventory.Transaction = transaction;
            insertInventory.CommandText =
                """
                INSERT INTO Inventaires (Id, BoutiqueId, UtilisateurId, CreeLe, EstValide)
                VALUES ($id, $boutiqueId, $userId, $created, 1);
                """;
            insertInventory.Parameters.AddWithValue("$id", inventoryId.ToString("D"));
            insertInventory.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            insertInventory.Parameters.AddWithValue("$userId", userId.ToString("D"));
            insertInventory.Parameters.AddWithValue("$created", now.ToString("O", CultureInfo.InvariantCulture));
            await insertInventory.ExecuteNonQueryAsync();
        }

        foreach (var entry in physicalQuantities)
        {
            var before = await GetProductQuantityAsync(connection, transaction, entry.Key, boutiqueId);
            if (before is null)
            {
                throw new InvalidOperationException("Un article de l’inventaire n’existe plus ou n’appartient pas à cette boutique.");
            }

            using (var insertLine = connection.CreateCommand())
            {
                insertLine.Transaction = transaction;
                insertLine.CommandText =
                    """
                    INSERT INTO LignesInventaire (Id, InventaireId, ProduitId, QuantiteTheorique, QuantitePhysique)
                    VALUES ($id, $inventoryId, $productId, $expected, $actual);
                    """;
                insertLine.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
                insertLine.Parameters.AddWithValue("$inventoryId", inventoryId.ToString("D"));
                insertLine.Parameters.AddWithValue("$productId", entry.Key.ToString("D"));
                insertLine.Parameters.AddWithValue("$expected", before.Value);
                insertLine.Parameters.AddWithValue("$actual", entry.Value);
                await insertLine.ExecuteNonQueryAsync();
            }

            if (before.Value != entry.Value)
            {
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE Produits SET QuantiteDisponible = $quantity WHERE Id = $id;";
                update.Parameters.AddWithValue("$quantity", entry.Value);
                update.Parameters.AddWithValue("$id", entry.Key.ToString("D"));
                await update.ExecuteNonQueryAsync();
                await InsertMovementAsync(
                    connection,
                    transaction,
                    boutiqueId,
                    entry.Key,
                    userId,
                    TypeMouvementStock.CorrectionInventaire,
                    entry.Value - before.Value,
                    before.Value,
                    entry.Value,
                    $"Correction d’inventaire du {now:dd/MM/yyyy}");
            }
        }

        using (var validateInventory = connection.CreateCommand())
        {
            validateInventory.Transaction = transaction;
            validateInventory.CommandText = "UPDATE Inventaires SET ValideLe = $validated WHERE Id = $id;";
            validateInventory.Parameters.AddWithValue("$validated", now.ToString("O", CultureInfo.InvariantCulture));
            validateInventory.Parameters.AddWithValue("$id", inventoryId.ToString("D"));
            await validateInventory.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<StockProfile> GetProfileAsync()
    {
        var (userId, boutiqueId) = await GetAccountIdsAsync();
        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT u.Nom, u.Identifiant, b.Nom, b.Telephone
            FROM Utilisateurs u
            INNER JOIN Boutiques b ON b.Id = u.BoutiqueId
            WHERE u.Id = $userId;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Le profil du compte est introuvable.");
        }

        return new StockProfile(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    public async Task<StockSummary> GetSummaryAsync()
    {
        var boutiqueId = await GetBoutiqueIdAsync();
        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(*),
                COALESCE(SUM(CASE WHEN QuantiteDisponible <= SeuilMinimum THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(QuantiteDisponible * PrixUnitaireFcfa), 0),
                (SELECT COUNT(*) FROM MouvementsStock WHERE BoutiqueId = $boutiqueId AND Type = 0),
                (SELECT COUNT(*) FROM MouvementsStock WHERE BoutiqueId = $boutiqueId AND Type = 1)
            FROM Produits
            WHERE BoutiqueId = $boutiqueId AND EstActif = 1;
            """;
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
        using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new StockSummary(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt64(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    private async Task<int?> GetProductQuantityAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid productId,
        Guid boutiqueId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT QuantiteDisponible FROM Produits WHERE Id = $id AND BoutiqueId = $boutiqueId AND EstActif = 1;";
        command.Parameters.AddWithValue("$id", productId.ToString("D"));
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
        var result = await command.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task InsertMovementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid boutiqueId,
        Guid productId,
        Guid userId,
        TypeMouvementStock type,
        int variation,
        int before,
        int after,
        string? reason)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO MouvementsStock
                (Id, BoutiqueId, ProduitId, UtilisateurId, Type, Variation, QuantiteAvant, QuantiteApres, Motif, CreeLe)
            VALUES
                ($id, $boutiqueId, $productId, $userId, $type, $variation, $before, $after, $reason, $created);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
        command.Parameters.AddWithValue("$productId", productId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$type", (int)type);
        command.Parameters.AddWithValue("$variation", variation);
        command.Parameters.AddWithValue("$before", before);
        command.Parameters.AddWithValue("$after", after);
        command.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> GetBoutiqueIdAsync() => (await GetAccountIdsAsync()).BoutiqueId;

    private async Task<(Guid UserId, Guid BoutiqueId)> GetAccountIdsAsync()
    {
        await database.InitializeAsync();
        var storedId = Preferences.Get("utilisateur_id", string.Empty);
        if (!Guid.TryParse(storedId, out var userId))
        {
            throw new InvalidOperationException("Votre session a expiré. Veuillez vous reconnecter.");
        }

        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT BoutiqueId FROM Utilisateurs WHERE Id = $userId;";
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var boutiqueId = (string?)await command.ExecuteScalarAsync();
        if (!Guid.TryParse(boutiqueId, out var parsedBoutiqueId))
        {
            throw new InvalidOperationException("La boutique associée à votre compte est introuvable.");
        }

        return (userId, parsedBoutiqueId);
    }
}

// ImagePath est le dernier paramètre et vaut null par défaut :
// les autres fichiers qui créent un StockProduct continuent de fonctionner.
public sealed record StockProduct(
    Guid Id,
    string Name,
    int Quantity,
    int MinimumQuantity,
    int UnitPrice,
    string? Category,
    string? ImagePath = null)
{
    public string StockStatus => Quantity == 0 ? "Rupture" : Quantity <= MinimumQuantity ? "Stock faible" : "En stock";
    public Color StockStatusColor => Quantity == 0
        ? Color.FromArgb("#D9534F")
        : Quantity <= MinimumQuantity
            ? Color.FromArgb("#B7831B")
            : Color.FromArgb("#35A65A");
    public Color CardBackground => Quantity == 0 ? Color.FromArgb("#E7F3FB") : Colors.White;
}

public sealed record StockMovement(
    Guid Id,
    string ProductName,
    TypeMouvementStock Type,
    int Variation,
    int QuantityBefore,
    int QuantityAfter,
    string? Reason,
    DateTimeOffset CreatedAt)
{
    public string TypeLabel => Type switch
    {
        TypeMouvementStock.Entree => "Entrée",
        TypeMouvementStock.Sortie => "Sortie",
        TypeMouvementStock.CorrectionInventaire => "Correction d’inventaire",
        _ => "Mouvement"
    };
}

public sealed record StockProfile(string Name, string Email, string StoreName, string Phone);

public sealed record StockSummary(int ProductCount, int AlertCount, long StockValue, int EntryCount, int ExitCount);