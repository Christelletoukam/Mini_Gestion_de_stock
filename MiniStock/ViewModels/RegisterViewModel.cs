using System.Net.Mail;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using MiniStock.Services;
using MiniStock.Views;

namespace MiniStock.ViewModels;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private string _nom = string.Empty;
    private string _nomBoutique = string.Empty;
    private string _email = string.Empty;
    private string _telephone = string.Empty;
    private string _motDePasse = string.Empty;
    private string _confirmationMotDePasse = string.Empty;
    private string _message = string.Empty;
    private Color _messageColor = Color.FromArgb("#D9534F");
    private bool _isPasswordHidden = true;
    private bool _isConfirmationHidden = true;
    private bool _isComplete;
    private bool _isBusy;

    public RegisterViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsBusy);
        BackCommand = new Command(async () => await _navigationService.PopAsync());
        TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
        ToggleConfirmationCommand = new Command(() => IsConfirmationHidden = !IsConfirmationHidden);
    }

    public string Nom
    {
        get => _nom;
        set => SetProperty(ref _nom, value);
    }

    public string NomBoutique
    {
        get => _nomBoutique;
        set => SetProperty(ref _nomBoutique, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Telephone
    {
        get => _telephone;
        set => SetProperty(ref _telephone, value);
    }

    public string MotDePasse
    {
        get => _motDePasse;
        set => SetProperty(ref _motDePasse, value);
    }

    public string ConfirmationMotDePasse
    {
        get => _confirmationMotDePasse;
        set => SetProperty(ref _confirmationMotDePasse, value);
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

    public bool IsConfirmationHidden
    {
        get => _isConfirmationHidden;
        set
        {
            if (SetProperty(ref _isConfirmationHidden, value))
            {
                OnPropertyChanged(nameof(ConfirmationVisibilityIcon));
            }
        }
    }

    public string PasswordVisibilityIcon =>
        IsPasswordHidden ? "lucide_eye.png" : "lucide_eye_off.png";

    public string ConfirmationVisibilityIcon =>
        IsConfirmationHidden ? "lucide_eye.png" : "lucide_eye_off.png";

    public bool IsComplete
    {
        get => _isComplete;
        private set
        {
            if (SetProperty(ref _isComplete, value))
            {
                OnPropertyChanged(nameof(TexteBouton));
            }
        }
    }

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

    public string TexteBouton => IsComplete ? "Se connecter" : "Créer mon compte";

    public ICommand SubmitCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand TogglePasswordCommand { get; }

    public ICommand ToggleConfirmationCommand { get; }

    private async Task SubmitAsync()
    {
        if (IsComplete)
        {
            await _navigationService.PopAsync();
            return;
        }

        Message = string.Empty;
        if (string.IsNullOrWhiteSpace(Nom) || string.IsNullOrWhiteSpace(NomBoutique))
        {
            SetError("Saisissez votre nom et le nom de la boutique.");
            return;
        }

        if (!MailAddress.TryCreate(Email.Trim(), out _))
        {
            SetError("Saisissez une adresse e-mail valide.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Telephone))
        {
            SetError("Saisissez un numéro de téléphone.");
            return;
        }

        if (MotDePasse.Length < 8)
        {
            SetError("Le mot de passe doit contenir au moins 8 caractères.");
            return;
        }

        if (MotDePasse != ConfirmationMotDePasse)
        {
            SetError("Les deux mots de passe ne correspondent pas.");
            return;
        }

        IsBusy = true;
        AuthResult result;
        try
        {
            result = await _authenticationService.RegisterAsync(
                Nom,
                NomBoutique,
                Email,
                Telephone,
                MotDePasse);
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

        Preferences.Set("last_registered_email", Email.Trim());
        MessageColor = Color.FromArgb("#35A65A");
        Message = "Compte créé. Vous pouvez maintenant vous connecter.";
        IsComplete = true;
    }

    private void SetError(string message)
    {
        MessageColor = Color.FromArgb("#D9534F");
        Message = message;
    }
}
