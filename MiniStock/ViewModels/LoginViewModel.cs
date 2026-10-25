using System.Net.Mail;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using MiniStock.Services;
using MiniStock.Views;

namespace MiniStock.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private string _email = string.Empty;
    private string _motDePasse = string.Empty;
    private string _message = string.Empty;
    private Color _messageColor = Color.FromArgb("#D9534F");
    private bool _isPasswordHidden = true;
    private bool _isBusy;

    public LoginViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;

        if (Preferences.ContainsKey("last_registered_email"))
        {
            Email = Preferences.Get("last_registered_email", string.Empty) ?? string.Empty;
        }
        else
        {
            Email = "demo@ministock.fr";
        }

        SubmitCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
        RegisterCommand = new Command(async () => await _navigationService.PushAsync<RegisterPage>());
        TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string MotDePasse
    {
        get => _motDePasse;
        set => SetProperty(ref _motDePasse, value);
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(AfficherMessage));
            }
        }
    }

    public Color MessageColor
    {
        get => _messageColor;
        private set => SetProperty(ref _messageColor, value);
    }

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set
        {
            if (SetProperty(ref _isPasswordHidden, value))
            {
                OnPropertyChanged(nameof(PasswordVisibilityIcon));
            }
        }
    }

    public string PasswordVisibilityIcon =>
        IsPasswordHidden ? "lucide_eye.png" : "lucide_eye_off.png";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value) && SubmitCommand is Command command)
            {
                command.ChangeCanExecute();
            }
        }
    }

    public bool AfficherMessage => !string.IsNullOrWhiteSpace(Message);

    public ICommand SubmitCommand { get; }

    public ICommand RegisterCommand { get; }

    public ICommand TogglePasswordCommand { get; }

    private async Task LoginAsync()
    {
        Message = string.Empty;
        if (!MailAddress.TryCreate(Email.Trim(), out _))
        {
            SetError("Saisissez une adresse e-mail valide.");
            return;
        }

        if (string.IsNullOrWhiteSpace(MotDePasse))
        {
            SetError("Saisissez votre mot de passe.");
            return;
        }

        IsBusy = true;
        AuthResult result;
        try
        {
            result = await _authenticationService.LoginAsync(Email, MotDePasse);
        }
        finally
        {
            IsBusy = false;
        }

        if (!result.IsSuccess)
        {
            SetError(result.Message);
            return;
        }

        Preferences.Set("utilisateur_id", result.UtilisateurId!.Value.ToString("D"));
        await _navigationService.SetRootPageAsync<MainPage>();
    }

    private void SetError(string message)
    {
        MessageColor = Color.FromArgb("#D9534F");
        Message = message;
    }
}
