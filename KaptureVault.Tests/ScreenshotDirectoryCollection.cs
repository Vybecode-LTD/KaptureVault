using Xunit;

namespace KaptureVault.Tests;

/// <summary>
/// Groups the test classes that mutate the process-wide static
/// <see cref="Kapture.Models.CaptureEntry.ScreenshotDirectory"/> (Phase 3 slice G) into one xUnit
/// collection, so they run sequentially with each other instead of racing the shared global across
/// parallel collections. (The collection still runs in parallel with everything else.)
/// </summary>
[CollectionDefinition("ScreenshotDirectory")]
public sealed class ScreenshotDirectoryCollection;
