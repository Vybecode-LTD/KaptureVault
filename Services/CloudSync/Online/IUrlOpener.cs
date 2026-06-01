namespace Kapture.Services.CloudSync.Online;

/// <summary>Opens a URL (Stripe Checkout/Portal) in the user's browser. Interface-backed so the
/// account view model is testable without launching a real browser.</summary>
public interface IUrlOpener
{
    void Open(string url);
}
