// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-016 (live): visual verification. The fixtures exercise the UI-016 palette with text labels and measured contrast;
// final acceptance needs the deployed exact-SHA screenshots at every required size.
namespace Causa.Acceptance.Live

open System
open System.Globalization
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PF016Tests =
    /// UI-016's canonical palette.
    let navy, teal, amber, coral, ivory = "#14283F", "#087F8C", "#E9A23B", "#D66B58", "#F5F3ED"

    /// WCAG 2.1 relative luminance and contrast ratio.
    let private luminance (hex: string) =
        let channel (i: int) =
            let c = float (Int32.Parse(hex.Substring(i, 2), NumberStyles.HexNumber)) / 255.0
            if c <= 0.03928 then c / 12.92 else Math.Pow((c + 0.055) / 1.055, 2.4)
        0.2126 * channel 1 + 0.7152 * channel 3 + 0.0722 * channel 5

    let contrast (a: string) (b: string) =
        let la, lb = luminance a, luminance b
        (max la lb + 0.05) / (min la lb + 0.05)

    /// The tone a stored state is shown in (UI-016: teal positive workflow, amber unresolved / pending / attention,
    /// coral contradictions and conflicts). Every tone is paired with the state's own text label.
    let toneOf (array: string) (state: string) =
        match array, state with
        | "relationships", "contradicts" -> Some coral
        | "contradictions", ("open" | "triaged" | "recorded") -> Some coral
        | "relationships", "supports" -> Some teal
        | "hypotheses", "supported" -> Some teal
        | "causes", "confirmed" -> Some teal
        | "correctiveActions", "verifiedEffective" -> Some teal
        | "unknowns", ("identified" | "investigating") -> Some amber
        | "hypotheses", ("proposed" | "plausible") -> Some amber
        | "correctiveActions", ("implemented" | "awaitingVerificationOfEffect") -> Some amber
        | _ -> None

    /// O1: the fixture scenarios, read from GitHub, populate teal, amber and coral states (navy and ivory are the
    /// structure and surface of every screen), each with a text label; the palette's pairs meet WCAG AA where they
    /// carry text (4.5:1) or are a relevant non-text indicator (3:1), and amber is never the only cue.
    let o1 (mk: string -> GitHubProvider) =
        let real = mk "pf016-o1"
        FixtureData.seed real (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        let tones =
            match ProviderPort.catalog real with
            | Error f -> failwith (ProviderPort.failureCode f)
            | Ok(_, rs) ->
                [ for r in rs do
                      match r.Result with
                      | Error f -> failwith (ProviderPort.failureCode f)
                      | Ok v ->
                          let state = ProviderPort.state v.Bytes.Events
                          for name in Projection.entityArrays do
                              match Canonical.field name state with
                              | Some(Canonical.JArray xs) ->
                                  for e in xs do
                                      match Canonical.str "state" e |> Option.orElse (Canonical.str "kind" e) with
                                      | Some label ->
                                          Assert.False(String.IsNullOrWhiteSpace label)
                                          match toneOf name label with
                                          | Some tone -> yield tone, label
                                          | None -> ()
                                      | None -> ()
                              | _ -> () ]
        for tone in [ teal; amber; coral ] do
            Assert.Contains(tones, fun (t, _) -> t = tone)
        Assert.All(tones, fun (_, label) -> Assert.Matches("^[a-zA-Z]+$", label))
        // Text on the workspace surface and on the masthead.
        Assert.True(contrast navy ivory >= 4.5, sprintf "navy on ivory %.2f" (contrast navy ivory))
        Assert.True(contrast ivory navy >= 4.5)
        // Non-text indicators (lozenge borders, markers) on ivory.
        Assert.True(contrast teal ivory >= 3.0, sprintf "teal on ivory %.2f" (contrast teal ivory))
        Assert.True(contrast coral ivory >= 3.0, sprintf "coral on ivory %.2f" (contrast coral ivory))
        // Amber is below 3:1 on ivory, so it is only used with its text label (DEC-R7-002).
        Assert.True(contrast amber ivory < 3.0)
        Assert.All(tones |> List.filter (fun (t, _) -> t = amber), fun (_, label) -> Assert.NotEmpty(label))

    /// O2: final acceptance has screenshots of the deployed candidate at its exact SHA at the desktop, iPad (portrait and
    /// landscape) and mobile sizes; local fixture screenshots alone do not satisfy it. Without them this is BLOCKED.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "pf016-o2"
        let env name = match Environment.GetEnvironmentVariable name with null | "" -> None | v -> Some v
        match env "CAUSA_SCREENSHOTS", env "CAUSA_CANDIDATE", env "CAUSA_APP_URL" with
        | Some dir, Some sha, Some url when IO.Directory.Exists dir ->
            Assert.Matches("^[0-9a-f]{40}$", sha)
            Assert.StartsWith("http", url)
            let shots = IO.Directory.EnumerateFiles(dir, "*.png", IO.SearchOption.AllDirectories) |> Seq.map (fun f -> string (IO.Path.GetFileName(f: string))) |> List.ofSeq
            for size in [ "desktop"; "ipad-portrait"; "ipad-landscape"; "mobile" ] do
                Assert.True(shots |> List.exists (fun f -> f.StartsWith(size + "-", StringComparison.Ordinal)), sprintf "no deployed %s screenshot of %s" size sha)
        | _ ->
            failwith "BLOCKED: no deployed exact-SHA screenshots (CAUSA_SCREENSHOTS, CAUSA_CANDIDATE, CAUSA_APP_URL) are available to this run; local fixture screenshots do not satisfy PF-016-O2. This is a failure, not a pass."

    [<Fact>]
    let ``PF-016-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-016-O2`` () = o2 PfKit.live
