using System.Runtime.CompilerServices;

namespace FlowableExternalWorkerClient.Tests;

/// <summary>
/// VCR-style HTTP recording/replay helper, similar to Python's VCR.py.
/// Creates a CassetteHandler that auto-records on first run and replays on subsequent runs.
/// Cassette files are stored in the source tree under Fixtures/Cassettes/.
/// </summary>
public static class Vcr
{
    public static CassetteHandler UseCassette(
        [CallerMemberName] string testName = "",
        [CallerFilePath] string callerFilePath = "")
    {
        var cassettesDir = Path.Combine(
            Path.GetDirectoryName(callerFilePath)!, "Fixtures", "Cassettes");
        var cassettePath = Path.Combine(cassettesDir, testName + ".json");
        return new CassetteHandler(cassettePath);
    }
}
