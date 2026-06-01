using System.Diagnostics;

namespace Kapture.Services.CloudSync.Online;

/// <summary>Opens a URL in the user's default browser via the shell. Untested-by-design (Process.Start),
/// like the rest of the browser/loopback boundary.</summary>
public sealed class BrowserUrlOpener : IUrlOpener
{
    public void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
