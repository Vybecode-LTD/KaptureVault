using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;

namespace Kapture.ViewModels;

/// <summary>The three things the login dialog can do.</summary>
public enum LoginMode
{
    SignIn,
    Register,
    Forgot,
}

/// <summary>
/// View model for the main-window <c>Login</c> dialog (Phase 5): sign in with email/password,
/// register a new account, request a password reset, or continue with Google. All account work
/// delegates to the tested <see cref="IOnlineAccountService"/>.
/// <para>
/// Account/vault-password interlock (§42): the Online Vault ACCOUNT password is distinct from the
/// vault ENCRYPTION password (the sole, unrecoverable key, derived locally and never sent). When
/// registering, an account password that equals the configured vault password is refused — an
/// account-password reset can never recover the vault, so they must not be the same secret.
/// </para>
/// </summary>
public partial class LoginDialogViewModel : ObservableObject
{
    private const int MinPasswordLength = 8;

    private readonly IOnlineAccountService _account;
    private readonly IEncryptionService _encryption;

    [ObservableProperty] private LoginMode _mode = LoginMode.SignIn;
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public LoginDialogViewModel(IOnlineAccountService account, IEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(encryption);
        _account = account;
        _encryption = encryption;
    }

    /// <summary>Raised when sign-in succeeds (Google or email/password) so the dialog can close.</summary>
    public event Action? SignedIn;

    public bool IsSignIn => Mode == LoginMode.SignIn;
    public bool IsRegister => Mode == LoginMode.Register;
    public bool IsForgot => Mode == LoginMode.Forgot;
    /// <summary>Password field shown for sign-in + register (not for the forgot-email step).</summary>
    public bool ShowPassword => Mode is LoginMode.SignIn or LoginMode.Register;
    /// <summary>Confirm-password + the interlock note are register-only.</summary>
    public bool ShowConfirm => Mode == LoginMode.Register;

    public string Heading => Mode switch
    {
        LoginMode.Register => "Create your account",
        LoginMode.Forgot => "Reset your password",
        _ => "Sign in to your Online Vault",
    };

    public string SubmitLabel => Mode switch
    {
        LoginMode.Register => "Create account",
        LoginMode.Forgot => "Send reset link",
        _ => "Sign in",
    };

    partial void OnModeChanged(LoginMode value)
    {
        StatusMessage = "";
        OnPropertyChanged(nameof(IsSignIn));
        OnPropertyChanged(nameof(IsRegister));
        OnPropertyChanged(nameof(IsForgot));
        OnPropertyChanged(nameof(ShowPassword));
        OnPropertyChanged(nameof(ShowConfirm));
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(SubmitLabel));
    }

    [RelayCommand] private void ShowSignIn() => Mode = LoginMode.SignIn;
    [RelayCommand] private void ShowRegister() => Mode = LoginMode.Register;
    [RelayCommand] private void ShowForgot() => Mode = LoginMode.Forgot;

    /// <summary>Submit the current mode's form (email/password sign-in, register, or reset-request).</summary>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        switch (Mode)
        {
            case LoginMode.SignIn: await SignInAsync(); break;
            case LoginMode.Register: await RegisterAsync(); break;
            case LoginMode.Forgot: await ForgotAsync(); break;
        }
    }

    private async Task SignInAsync()
    {
        if (!ValidEmail() || Password.Length == 0)
        {
            StatusMessage = "Enter your email and password.";
            return;
        }

        await RunBusy(async () =>
        {
            if (await _account.SignInWithPasswordAsync(Email.Trim(), Password))
            {
                Succeed();
            }
            else if (_account.NeedsVerification)
            {
                StatusMessage = "Please verify your email first — check your inbox for the verification link.";
            }
            else
            {
                StatusMessage = _account.LastError ?? "Couldn't sign in.";
            }
        });
    }

    private async Task RegisterAsync()
    {
        if (!ValidEmail())
        {
            StatusMessage = "Enter a valid email address.";
            return;
        }
        if (Password.Length < MinPasswordLength)
        {
            StatusMessage = $"Choose a password of at least {MinPasswordLength} characters.";
            return;
        }
        if (Password != ConfirmPassword)
        {
            StatusMessage = "The passwords don't match.";
            return;
        }
        // §42 interlock: the account password must NOT be the vault encryption password.
        if (_encryption.IsConfigured && _encryption.VerifyPassword(Password))
        {
            StatusMessage =
                "Your account password must be different from your vault encryption password. " +
                "The account password lets you sign in and can be reset by email; your vault password " +
                "is the only key to your encrypted data and can never be recovered.";
            return;
        }

        await RunBusy(async () =>
        {
            if (await _account.RegisterAsync(Email.Trim(), Password))
            {
                Password = "";
                ConfirmPassword = "";
                Mode = LoginMode.SignIn; // clears StatusMessage (OnModeChanged) — set the message AFTER
                StatusMessage = "Account created. Check your email for a verification link, then sign in.";
            }
            else
            {
                StatusMessage = _account.LastError ?? "Couldn't create the account.";
            }
        });
    }

    private async Task ForgotAsync()
    {
        if (!ValidEmail())
        {
            StatusMessage = "Enter the email address for your account.";
            return;
        }

        await RunBusy(async () =>
        {
            await _account.RequestPasswordResetAsync(Email.Trim());
            Mode = LoginMode.SignIn; // clears StatusMessage (OnModeChanged) — set the message AFTER
            // Deliberately neutral — never reveals whether the email maps to an account.
            StatusMessage = "If that email has an account, we've sent a password-reset link.";
        });
    }

    [RelayCommand]
    private async Task ContinueWithGoogleAsync()
    {
        await RunBusy(async () =>
        {
            StatusMessage = "Opening your browser to sign in…";
            if (await _account.SignInAsync())
                Succeed();
            else
                StatusMessage = _account.LastError ?? "Sign-in was cancelled.";
        });
    }

    private async Task RunBusy(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Succeed()
    {
        // Pull /me so the toolbar reflects entitlement/quota immediately, then close the dialog.
        _ = _account.RefreshAccountAsync();
        SignedIn?.Invoke();
    }

    private bool ValidEmail()
    {
        var e = Email.Trim();
        return e.Length > 0 && e.Contains('@') && e.Contains('.') && !e.Contains(' ');
    }
}
