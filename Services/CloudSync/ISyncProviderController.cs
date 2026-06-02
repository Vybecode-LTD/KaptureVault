namespace Kapture.Services.CloudSync;

/// <summary>
/// Minimal seam over <see cref="CloudSyncManager"/> for selecting the active sync provider live, so
/// the Settings "Online Vault" panel can make the Online Vault the sync target without an app
/// restart (the gap found in the F-02 Phase 3 smoke: a provider change only took effect at startup).
/// Implemented by CloudSyncManager; an interface so the view model stays unit-testable.
/// </summary>
public interface ISyncProviderController
{
    /// <summary>The <c>ProviderName</c> of the currently active sync provider, or null if none is selected.</summary>
    string? ActiveProviderName { get; }

    /// <summary>Select the active sync provider by its <c>ProviderName</c> (null clears the selection).</summary>
    void SetActiveProvider(string? providerName);
}
