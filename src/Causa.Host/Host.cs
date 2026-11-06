// Owner: integration. [JSExport] glue only: every decision is the F# engine's (Causa.App.Program).
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Causa.Host;

[SupportedOSPlatform("browser")]
public static partial class Host
{
    /// One Limen BrowserToEngineMessage (JSON) in, one EngineToBrowserMessage (JSON) out.
    [JSExport]
    public static string Dispatch(string messageJson) => Causa.App.Program.dispatch(messageJson);

    public static void Main()
    {
    }
}
