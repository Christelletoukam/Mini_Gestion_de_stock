using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MiniStock.Data;

namespace MiniStock.Services;

public sealed class AuthenticationService(StockDatabase database) : IAuthenticationService
{
    private const int PasswordIterations = 210_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return string.Join(
            '$',
            "pbkdf2-sha256",
            PasswordIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public async Task<AuthResult> RegisterAsync(
        string nom,
        string nomBoutique,
        string email,
        string telephone,
        string motDePasse)
    {
        await database.InitializeAsync();

        var normalizedEmail = email.Trim();
        var boutiqueId = Guid.NewGuid();
        var utilisateurId = Guid.NewGuid();
        var passwordHash = await Task.Run(() => HashPassword(motDePasse));

        using var connection = database.CreateConnection();
        using var transaction = connection.BeginTransaction();

        using (var checkEmail = connection.CreateCommand())
        {
            checkEmail.Transaction = transaction;
            checkEmail.CommandText = "SELECT 1 FROM Utilisateurs WHERE Identifiant = $email LIMIT 1;";
            checkEmail.Parameters.AddWithValue("$email", normalizedEmail);
            if (checkEmail.ExecuteScalar() is not null)
            {
                return new AuthResult(false, "Un compte utilise déjà cette adresse e-mail.");
            }
        }

        using (var boutiqueCommand = connection.CreateCommand())
        {
            boutiqueCommand.Transaction = transaction;
            boutiqueCommand.CommandText =
                """
                INSERT INTO Boutiques (Id, Nom, Telephone, Adresse, CreeLe)
                VALUES ($id, $nom, $telephone, NULL, $creeLe);
                """;
            boutiqueCommand.Parameters.AddWithValue("$id", boutiqueId.ToString("D"));
            boutiqueCommand.Parameters.AddWithValue("$nom", nom.Trim());
            boutiqueCommand.Parameters.AddWithValue("$telephone", telephone.Trim());
            boutiqueCommand.Parameters.AddWithValue("$creeLe", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            boutiqueCommand.ExecuteNonQuery();
        }

        using (var utilisateurCommand = connection.CreateCommand())
        {
            utilisateurCommand.Transaction = transaction;
            utilisateurCommand.CommandText =
                """
                INSERT INTO Utilisateurs (Id, BoutiqueId, Nom, Identifiant, MotDePasseHash, CreeLe)
                VALUES ($id, $boutiqueId, $nom, $email, $motDePasseHash, $creeLe);
                """;
            utilisateurCommand.Parameters.AddWithValue("$id", utilisateurId.ToString("D"));
            utilisateurCommand.Parameters.AddWithValue("$boutiqueId", boutiqueId.ToString("D"));
            utilisateurCommand.Parameters.AddWithValue("$nom", nom.Trim());
            utilisateurCommand.Parameters.AddWithValue("$email", normalizedEmail);
            utilisateurCommand.Parameters.AddWithValue("$motDePasseHash", passwordHash);
            utilisateurCommand.Parameters.AddWithValue("$creeLe", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            utilisateurCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return new AuthResult(true, "Compte créé.", utilisateurId);
    }

    public async Task<AuthResult> LoginAsync(string email, string motDePasse)
    {
        await database.InitializeAsync();

        using var connection = database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, MotDePasseHash
            FROM Utilisateurs
            WHERE Identifiant = $email
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", email.Trim());

        string? utilisateurId;
        string? storedHash;
        using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                return new AuthResult(false, "Adresse e-mail ou mot de passe incorrect.");
            }

            utilisateurId = reader.GetString(0);
            storedHash = reader.GetString(1);
        }

        if (!VerifyPassword(motDePasse, storedHash))
        {
            return new AuthResult(false, "Adresse e-mail ou mot de passe incorrect.");
        }

        if (!Guid.TryParse(utilisateurId, out var parsedUtilisateurId))
        {
            throw new InvalidDataException("L’identifiant du compte enregistré est invalide.");
        }

        return new AuthResult(true, "Connexion réussie.", parsedUtilisateurId);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 ||
            parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations is < 10_000 or > 1_000_000)
        {
            throw new InvalidDataException("Le format du mot de passe enregistré est invalide.");
        }

        var salt = new byte[SaltLength];
        var expectedHash = new byte[HashLength];
        if (!Convert.TryFromBase64String(parts[2], salt, out var saltLength) ||
            saltLength != SaltLength ||
            !Convert.TryFromBase64String(parts[3], expectedHash, out var hashLength) ||
            hashLength != HashLength)
        {
            throw new InvalidDataException("Les données de sécurité du compte sont invalides.");
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
