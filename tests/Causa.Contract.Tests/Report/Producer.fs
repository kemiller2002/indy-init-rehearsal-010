// Owner: ui-3. IF41-REPORT producer contract, and the ui-3 analysis-section views checked against real kernel states
// (Causa.TestSupport.UiFixtures.AnalysisScenarios builds every investigation through Kernel.domain.Decide/Evolve).
namespace Causa.Contract.Tests.Report

open System
open System.IO
open System.Text.RegularExpressions
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui
open Causa.App.Workspace
open Causa.App.Views
open Causa.App.Views.Workspace
open Causa.TestSupport.UiFixtures

module Producer =
    module S = AnalysisScenarios

    let private withCause () =
        S.continueWith
            (S.analysed ())
            [ RecordCausalAssertion
                  { CauseId = CausalAssertionId(S.g 60); Outcome = S.t "Checkout payments fail"
                    Members = [ { HypothesisId = S.h1; Role = CauseRole.RootCause } ]; Mechanism = None } ]

    /// Boundary E: report rendering cannot elevate an unverified hypothesis to a confirmed cause.
    [<Fact>]
    let UnverifiedNeverConfirmed () =
        let s = withCause ()
        Assert.Equal(CauseState.Proposed, s.Causes[CausalAssertionId(S.g 60)].State)
        let report = Report.reporting.Project(Kernel.domain.Project s)
        Assert.Equal(s.Id, report.Investigation)
        Assert.Equal(s.Version, report.Version)
        let paragraphs = report.Sections |> List.collect (fun x -> x.Paragraphs)
        // Nothing in the report presents the proposed cause, or any hypothesis, as confirmed.
        Assert.DoesNotContain(paragraphs, fun p -> p.StartsWith("Confirmed cause", StringComparison.OrdinalIgnoreCase))
        let causes = report.Sections |> List.find (fun x -> x.Heading = "Causes")
        Assert.Equal<string list>([ "Causal assertion (proposed, not confirmed): Checkout payments fail. Members: The TLS certificate expired (root cause)" ], causes.Paragraphs)
        Assert.Contains("Cause not confirmed: Checkout payments fail", report.UnresolvedUncertainties)
        let hypotheses = report.Sections |> List.find (fun x -> x.Heading = "Hypotheses")
        Assert.Contains($"{S.h1Statement} (hypothesis, proposed)", hypotheses.Paragraphs)
        // Every section cites the events it reports.
        Assert.NotEmpty(causes.Provenance)
        Assert.All(report.Sections |> List.collect (fun x -> x.Provenance), fun id -> Assert.Contains(id, s.History |> List.map (fun e -> e.EventId)))

module WorkspaceViews =
    module S = AnalysisScenarios

    let private draftsOf (m: Map<string, string>) = fun k -> m |> Map.tryFind k |> Option.defaultValue ""
    let private noDrafts = draftsOf Map.empty

    let private value (keys: (string * ViewValue) list) (k: string) =
        match keys |> List.tryFind (fst >> (=) k) with
        | Some(_, v) -> v
        | None -> failwithf "key %s is not emitted" k

    let private text keys k =
        match value keys k with
        | BindingValue.Text t -> t
        | v -> failwithf "%s is not text: %A" k v

    let private flag keys k =
        match value keys k with
        | BindingValue.Flag b -> b
        | v -> failwithf "%s is not a flag: %A" k v

    let private rows keys k =
        match value keys k with
        | BindingValue.Items rs -> rs
        | v -> failwithf "%s is not a list: %A" k v

    let private cell (row: Map<string, BindingPrimitive>) (field: string) =
        match row.TryFind field with
        | Some(BindingPrimitive.Text t) -> t
        | Some(BindingPrimitive.Number n) -> string n
        | Some(BindingPrimitive.Flag b) -> string b
        | None -> failwithf "row has no field %s" field

    let private rowFlag (row: Map<string, BindingPrimitive>) field = row.[field] = BindingPrimitive.Flag true

    let private allViews (draft: string -> string) (s: Investigation option) =
        Overview.keys draft s @ Hypotheses.keys draft s @ Evidence.keys draft s @ Links.keys draft s
        @ Conclusions.keys draft s @ Timeline.keys s @ Graph.keys s

    [<Fact>]
    let ``INV-007 INV-008 INV-018 overview shows creation time, original problem statement and state`` () =
        let s = S.analysed ()
        let keys = Overview.keys noDrafts (Some s)
        Assert.True(flag keys "investigation.overview.loaded")
        Assert.Equal(S.problem, text keys "investigation.overview.problem")
        Assert.Equal(S.title, text keys "investigation.overview.title")
        Assert.Equal(Words.instant s.CreatedAt, text keys "investigation.overview.created")
        Assert.Equal("2026-11-06T12:01:00Z", text keys "investigation.overview.created")
        Assert.Equal("active", text keys "investigation.overview.state")
        Assert.Equal("draft", text (Overview.keys noDrafts (Some(S.started ()))) "investigation.overview.state")

    [<Fact>]
    let ``INV-014 INV-024 INV-025 observations list their text and the engine offers Record an observation`` () =
        let keys = Overview.keys (draftsOf (Map [ Overview.observationText, "typed" ])) (Some(S.analysed ()))
        let observations = rows keys "investigation.overview.observations"
        // The problem statement is shown as such, not repeated as an observation.
        Assert.Equal<string list>([ S.observationText ], observations |> List.map (fun r -> cell r "text"))
        // causa-storage v1.0.0 has no observation source (OD-HC-06): none is shown rather than one invented.
        Assert.All(observations, fun r -> Assert.False(rowFlag r "hasSource"))
        Assert.True(flag keys "investigation.observation.record.offered")
        Assert.False(flag keys "investigation.observation.record.disabled")
        Assert.Equal("typed", text keys Overview.observationText)
        Assert.Equal("", text keys Overview.observationSource)

    [<Fact>]
    let ``INV-009 INV-010 INV-011 INV-026 hypotheses list statement, state and explanation`` () =
        let keys = Hypotheses.keys noDrafts (Some(S.analysed ()))
        let items = rows keys "investigation.hypotheses.items"
        Assert.Equal<string list>([ S.h1Statement; S.h2Statement ], items |> List.map (fun r -> cell r "statement"))
        Assert.Equal<string list>([ "proposed"; "proposed" ], items |> List.map (fun r -> cell r "state"))
        Assert.Equal(S.h1Explanation, cell items.[0] "explanation")
        Assert.False(rowFlag items.[1] "hasExplanation")
        Assert.True(flag keys "investigation.hypothesis.record.offered")
        Assert.False(flag keys "investigation.hypothesis.record.disabled")
        Assert.True(flag (Hypotheses.keys noDrafts (Some(S.started ()))) "investigation.hypotheses.empty")

    [<Fact>]
    let ``INV-012 INV-013 INV-027 INV-028 INV-029 evidence shows content, source and only a safe URL as a link`` () =
        let keys = Evidence.keys noDrafts (Some(S.analysed ()))
        let items = rows keys "investigation.evidence.items"
        Assert.Equal<string list>([ S.e1Content; S.e2Content ], items |> List.map (fun r -> cell r "content"))
        Assert.Equal<string list>([ S.e1Source; S.e2Source ], items |> List.map (fun r -> cell r "source"))
        // No label field exists in causa-storage v1.0.0 (OD-HC-06): the IF-UI wording, never an invented label.
        Assert.All(items, fun r -> Assert.Equal(Evidence.noLabel, cell r "label"))
        Assert.True(rowFlag items.[0] "hasUrl")
        Assert.Equal(S.e1Source, cell items.[0] "url")
        Assert.False(rowFlag items.[1] "hasUrl")
        Assert.Equal("", cell items.[1] "url")
        Assert.True(flag keys "investigation.evidence.record.offered")

    [<Fact>]
    let ``INV-030 INV-031 INV-038 INV-044 a link is shown on both the evidence and the hypothesis`` () =
        let s = Some(S.analysed ())
        let evidence = rows (Evidence.keys noDrafts s) "investigation.evidence.items"
        let hypotheses = rows (Hypotheses.keys noDrafts s) "investigation.hypotheses.items"
        Assert.Equal($"supports: {S.h1Statement} — {S.r1}", cell evidence.[0] "links")
        Assert.Equal($"contradicts: {S.h1Statement} — {S.r2}", cell evidence.[1] "links")
        Assert.Equal($"supports: {S.e1Content} — {S.r1}\ncontradicts: {S.e2Content} — {S.r2}", cell hypotheses.[0] "links")
        Assert.False(rowFlag hypotheses.[1] "hasLinks")
        let links = Links.keys noDrafts s
        Assert.True(flag links "investigation.link.relate.offered")
        Assert.True(flag links "investigation.link.reassess.offered")
        Assert.Equal<string list>([ S.e1Content; S.e2Content ], rows links "investigation.links.evidenceOptions" |> List.map (fun r -> cell r "label"))
        Assert.Equal<string list>([ S.h1Statement; S.h2Statement ], rows links "investigation.links.hypothesisOptions" |> List.map (fun r -> cell r "label"))
        // INV-031: no kind is selected until the user chooses one; the choice is read back from its draft.
        Assert.All(Analysis.kindOptions, fun o -> Assert.False(flag links (Links.kindChecked o)))
        let chosen = Links.keys (draftsOf (Map [ Links.kind, "contradicts"; Links.evidence, Ids.text (S.g 31) ])) s
        Assert.True(flag chosen (Links.kindChecked "contradicts"))
        Assert.False(flag chosen (Links.kindChecked "supports"))
        Assert.Equal<bool list>([ false; true ], rows chosen "investigation.links.evidenceOptions" |> List.map (fun r -> rowFlag r "selected"))
        Assert.Equal<string list>([ "supports"; "contradicts"; "consistent-with"; "inconclusive" ], Analysis.kindOptions)

    [<Fact>]
    let ``INV-035 reassessing changes the kind and keeps the earlier assessment in the history`` () =
        let s = Some(S.reassessed ())
        let evidence = rows (Evidence.keys noDrafts s) "investigation.evidence.items"
        Assert.Equal($"contradicts: {S.h1Statement} — {S.r3}", cell evidence.[0] "links")
        let history = rows (Links.keys noDrafts s) "investigation.links.history"
        Assert.Equal<(string * string * string) list>(
            [ "supports", S.r1, "earlier assessment"; "contradicts", S.r2, "current"; "contradicts", S.r3, "current" ],
            history |> List.map (fun r -> cell r "kind", cell r "rationale", cell r "status"))
        let timeline = rows (Timeline.keys s) "investigation.timeline.items" |> List.map (fun r -> cell r "summary")
        Assert.Contains($"Assessment recorded as supports: {S.e1Content} supports {S.h1Statement}. Rationale: {S.r1}", timeline)
        Assert.Contains($"Assessment revised from supports to contradicts: {S.e1Content} contradicts {S.h1Statement}. Rationale: {S.r3}", timeline)

    [<Fact>]
    let ``INV-032 Record a conclusion is not offered because the frozen contract has no command for it`` () =
        let keys = Conclusions.keys noDrafts (Some(S.analysed ()))
        Assert.False(flag keys "investigation.conclusion.record.offered")
        Assert.True(flag keys "investigation.conclusions.empty")
        Assert.Empty(rows keys "investigation.conclusions.items")

    [<Fact>]
    let ``INV-033 conclusions are rendered from the history`` () =
        let keys = Conclusions.keys noDrafts (Some(S.withStoredConclusion (S.analysed ())))
        let items = rows keys "investigation.conclusions.items"
        Assert.Equal<string list>([ S.conclusionStatement ], items |> List.map (fun r -> cell r "statement"))
        Assert.Equal("The certificate renewal log is incomplete", cell items.[0] "residual")
        Assert.False(flag keys "investigation.conclusions.empty")
        Assert.False(flag keys "investigation.conclusion.record.offered")

    [<Fact>]
    let ``INV-042 J2-08 every section shows this investigation's content`` () =
        let s = Some(S.analysed ())
        Assert.Equal(S.problem, text (Overview.keys noDrafts s) "investigation.overview.problem")
        let causal = rows (Graph.keys s) "investigation.causal.relationships"
        Assert.Contains(S.r2, causal |> List.map (fun r -> cell r "rationale"))
        Assert.Equal<string list>([ "supports"; "contradicts" ], causal |> List.map (fun r -> cell r "kind"))
        Assert.Contains(S.e2Content, rows (Evidence.keys noDrafts s) "investigation.evidence.items" |> List.map (fun r -> cell r "content"))
        Assert.Contains(S.h2Statement, rows (Hypotheses.keys noDrafts s) "investigation.hypotheses.items" |> List.map (fun r -> cell r "statement"))
        let timeline = rows (Timeline.keys s) "investigation.timeline.items"
        Assert.Equal(int (AggregateVersion.value s.Value.Version), timeline.Length)
        Assert.Contains($"Observation recorded: {S.observationText}", timeline |> List.map (fun r -> cell r "summary"))

    [<Fact>]
    let ``Every section emits its full key set whether or not the investigation is loaded`` () =
        let loaded = allViews noDrafts (Some(S.analysed ())) |> List.map fst |> List.sort
        let missing = allViews noDrafts None |> List.map fst |> List.sort
        Assert.Equal<string list>(loaded, missing)
        Assert.Equal(loaded.Length, (List.distinct loaded).Length)
        Assert.All(loaded, fun k -> Assert.StartsWith("investigation.", k))
        let none = allViews noDrafts None
        Assert.All([ "overview"; "hypotheses"; "evidence"; "timeline"; "causal" ], fun sec -> Assert.True(flag none $"investigation.{sec}.missing"))
        Assert.False(flag none "investigation.observation.record.offered")

    [<Fact>]
    let ``The views read the investigation the route names from the verified load`` () =
        let s = S.analysed ()
        let address = (WorkspaceAddress.workingCopy [] "ui3-test").Value
        let (Workspace workspace) = address.Workspace
        let hash = $"#/investigation/{Uri.EscapeDataString workspace}/{Ids.text (S.g 1)}/evidence"
        let state, _ = Update.init hash
        let verified: VerifiedInvestigation =
            { Investigation = s; History = List.rev s.History; ReadAt = CommitOid "0000000000000000000000000000000000000001"
              Address = address; Bytes = { Manifest = [||]; Events = [||]; Snapshot = [||] } }
        let ref: InvestigationRef = { Address = address; Id = s.Id }
        let withLoad = { state with Model = { state.Model with Loads = Map [ ref, Loaded verified ] } }
        Assert.Equal(None, Analysis.loaded state)
        Assert.Equal(Some s, Analysis.loaded withLoad)
        Assert.Equal<(string * ViewValue) list>(Evidence.keys noDrafts (Some s), Evidence.view withLoad)
        let other, _ = Update.init $"#/investigation/{Uri.EscapeDataString workspace}/{Ids.text (S.g 99)}/evidence"
        Assert.Equal(None, Analysis.loaded { other with Model = withLoad.Model })

    /// The fragments' bindings, by scope: top-level keys, and per data-each list the row fields it binds.
    let private repoRoot () =
        let rec up (d: string) =
            if Directory.Exists(Path.Combine(d, "web", "screens")) then d
            else
                match Path.GetDirectoryName d with
                | null -> failwith "repository root not found"
                | parent when parent = d -> failwith "repository root not found"
                | parent -> up parent
        up (Path.GetFullPath AppContext.BaseDirectory)

    let private bindings (html: string) =
        let tag = Regex(@"<(/?)([a-zA-Z0-9]+)([^>]*)>")
        let attr = Regex(@"data-(text|if|each|bind-[a-z-]+)=""([^""]+)""")
        tag.Matches html
        |> Seq.fold
            (fun (stack: string option list, found: (string option * string) list) m ->
                let closing, name = m.Groups[1].Value = "/", m.Groups[2].Value.ToLowerInvariant()
                if name = "template" && closing then List.tail stack, found
                else
                    let scope = stack |> List.tryPick id
                    let attrs = attr.Matches m.Groups[3].Value |> Seq.map (fun a -> a.Groups[1].Value, a.Groups[2].Value) |> List.ofSeq
                    let found = found @ (attrs |> List.map (fun (_, k) -> scope, k))
                    if name = "template" then
                        let each = attrs |> List.tryPick (fun (kind, k) -> if kind = "each" then Some k else None)
                        each :: stack, found
                    else stack, found)
            ([], [])
        |> snd

    [<Fact>]
    let ``Every key the ui-3 fragments bind is emitted by the views`` () =
        let views = allViews noDrafts (Some(S.full ()))
        let keys = views |> List.map fst |> Set.ofList
        let files = [ "workspace-overview"; "workspace-hypotheses"; "workspace-evidence"; "workspace-causal-analysis"; "workspace-timeline" ]
        let found = files |> List.collect (fun f -> bindings (File.ReadAllText(Path.Combine(repoRoot (), "web", "screens", f + ".html"))))
        Assert.NotEmpty(found)
        for scope, k in found do
            match scope with
            | None when k.StartsWith("screen.investigation.", StringComparison.Ordinal) -> ()
            | None -> Assert.True(keys.Contains k, $"top-level key {k} is not emitted")
            | Some list ->
                match value views list with
                | BindingValue.Items(first :: _) -> Assert.True(first.ContainsKey k, $"row field {k} of {list} is not emitted")
                | other -> failwithf "%s has no rows to check: %A" list other
