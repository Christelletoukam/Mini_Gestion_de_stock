namespace MiniStock.Services;

public interface IAuthenticationService
{
    Task<AuthResult> RegisterAsync(
        string nom,
        string nomBoutique,
        string email,
        string telephone,
        string motDePasse);

    Task<AuthResult> LoginAsync(string email, string motDePasse);
}

public sealed record AuthResult(bool IsSuccess, string Message, Guid? UtilisateurId = null);
