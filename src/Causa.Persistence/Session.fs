// Owner: persistence. The workspace lister (v4.1.5 L1) and the persistence entry points the App engine calls.
namespace Causa.Persistence

open System
open Causa.Domain

/// v4.1.5 L1, W1: listing a workspace of the connected repository at one commit. Nothing is written (EXT-DATA-006).
[<RequireQualifiedAccess>]
module WorkspaceListing =
    open System.Text.Json

    let private prop (name: string) (e: JsonElement) =
        if e.ValueKind = JsonValueKind.Object then
            match e.TryGetProperty name with
            | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
            | _ -> None
        else None

    let private str name e = prop name e |> Option.bind (fun v -> if v.ValueKind = JsonValueKind.String then Option.ofObj (v.GetString()) else None)

    let private uuid = Text.RegularExpressions.Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z")

    let private failed (e: ProviderError) = ListingFailed(ProviderFailed e)

    let private malformed (detail: string) = ListingFailed(ProviderFailed(Malformed detail))

    /// `catalog/<name>/manifest.json` for the catalog workspace `workspaces/demo/<name>`.
    let manifestPathOf (Workspace w) = sprintf "catalog/%s/manifest.json" (w.Split('/') |> Array.last)

    let private catalogPath = "catalog/active.json"

    let private parseJson (bytes: byte[]) =
        try
            use doc = JsonDocument.Parse(bytes: byte[])
            Some(doc.RootElement.Clone())
        with _ -> None

    let private list (ctx: RequestContext) (_: ListingTarget) : ListingStep * HttpRequest =
        ListHead, GitHubRequests.branchHead ctx.NextId ctx.Connection

    let private interpret (ctx: RequestContext) (target: ListingTarget) (step: ListingStep) (outcome: HttpOutcome) : ListingInterpretation =
        match step with
        | ListHead ->
            match Classify.answer ReadBranchHead outcome |> Result.map Classify.headOid with
            | Ok(Some head) ->
                match target with
                | ActiveCatalog ->
                    ListingNextRequest(ListActiveCatalogAt head, GitHubRequests.fileAt ctx.NextId ctx.Connection head (RepoPath catalogPath))
                | WorkingCopyOf address ->
                    let (Workspace w) = address.Workspace
                    ListingNextRequest(ListDirectoryAt(head, address.Workspace), GitHubRequests.fileAt ctx.NextId ctx.Connection head (RepoPath(w + "/investigations")))
            | Ok None -> malformed "the branch answer has no head"
            | Error e -> failed e
        | ListActiveCatalogAt head ->
            match Classify.answer (ReadFileAt(RepoPath catalogPath, head)) outcome with
            | Error e -> failed e
            | Ok answer ->
                match Classify.fileBytes answer |> Option.bind parseJson with
                | None -> malformed "catalog/active.json does not decode"
                | Some active ->
                    match str "workspace" active, str "manifest" active with
                    | Some w, Some manifest when manifest = manifestPathOf (Workspace w) ->
                        ListingNextRequest(ListManifestAt(head, Workspace w), GitHubRequests.fileAt ctx.NextId ctx.Connection head (RepoPath manifest))
                    | _ -> malformed "catalog/active.json names no workspace and manifest of the catalog layout"
        | ListManifestAt(head, workspace) ->
            match Classify.answer (ReadFileAt(RepoPath(manifestPathOf workspace), head)) outcome with
            | Error e -> failed e
            | Ok answer ->
                match Classify.fileBytes answer |> Option.bind parseJson with
                | None -> malformed "the catalog manifest does not decode"
                | Some manifest when str "workspace" manifest <> Some(let (Workspace w) = workspace in w) ->
                    malformed "the catalog manifest names another workspace"
                | Some manifest ->
                    let address = WorkspaceAddress.catalog workspace
                    let (Workspace w) = workspace
                    let cases =
                        match prop "cases" manifest with
                        | Some c when c.ValueKind = JsonValueKind.Array -> [ for x in c.EnumerateArray() -> x ]
                        | _ -> []
                    let listed =
                        cases
                        |> List.choose (fun c ->
                            match str "investigationId" c with
                            | Some id when uuid.IsMatch id ->
                                let path = sprintf "%s/investigations/%s" w id
                                if str "path" c = Some path then
                                    Some
                                        { Ref = { Address = address; Id = InvestigationId(Guid id) }
                                          Path = RepoPath path
                                          Title = str "title" c
                                          ExpectedStateSha256 = str "expectedStateSha256" c }
                                else None
                            | _ -> None)
                    if listed.Length <> cases.Length then malformed "a catalog case has no valid id or path"
                    else ListingComplete { Address = address; ReadAt = head; Investigations = listed }
        | ListDirectoryAt(head, workspace) ->
            let address =
                match target with
                | WorkingCopyOf a -> a
                | ActiveCatalog -> WorkspaceAddress.catalog workspace
            let (Workspace w) = workspace
            match Classify.answer (ReadFileAt(RepoPath(w + "/investigations"), head)) outcome with
            // EXT-DATA-009: a working copy that does not exist yet is a clean start.
            | Error(NotFound _) -> ListingComplete { Address = address; ReadAt = head; Investigations = [] }
            | Error e -> failed e
            | Ok answer ->
                match parseJson (Text.Encoding.UTF8.GetBytes answer.Body) with
                | Some entries when entries.ValueKind = JsonValueKind.Array ->
                    let listed =
                        [ for e in entries.EnumerateArray() do
                              match str "type" e, str "name" e with
                              | Some "dir", Some name when uuid.IsMatch name ->
                                  yield
                                      { Ref = { Address = address; Id = InvestigationId(Guid name) }
                                        Path = RepoPath(sprintf "%s/investigations/%s" w name)
                                        Title = None
                                        ExpectedStateSha256 = None }
                              | _ -> () ]
                    ListingComplete { Address = address; ReadAt = head; Investigations = listed }
                | _ -> malformed "the investigations directory answer is not a listing"

    let lister: WorkspaceLister = { List = list; InterpretList = interpret }

/// The persistence entry points the App engine uses. Every request is IF41-WIRE data (HttpRequest) that the engine
/// turns into a Limen Http effect; every answer comes back as HttpOutcome and is decoded here.
[<RequireQualifiedAccess>]
module Persistence =
    /// SM-SAVE / SM-CONFLICT / SM-LOAD is `saveMachine` (SaveMachineValue), the one public SaveMachine value; it is
    /// not re-exported here, so exactly one exists (PERSIST-023-O1).
    let private machine () : SaveMachine = saveMachine

    /// Connect (EXT-DATA-005).
    let connector: Connector = Classify.connector

    /// Listing a workspace (L1).
    let lister: WorkspaceLister = WorkspaceListing.lister

    /// The causa-storage v1.0.0 codec (IF41-STORAGE).
    let codec: StorageCodec = StorageReader.codec Kernel.domain

    /// The working copy `workspaces/demo/causa-app-<run>` of this tab, never a catalog workspace.
    let workingCopy (catalogs: Workspace list) (run: string) : Writable.T option =
        WorkspaceAddress.workingCopy catalogs run |> Option.bind Writable.ofAddress

    /// Opening an investigation (SM-LOAD OpenRequested): online, the load state and the one request that reads the
    /// branch head and the investigation's files at it (T-L01); known offline, UnavailableOffline and nothing sent
    /// (T-L02). From LoadFailed it reads again with a new request identity (T-L08).
    let openInvestigation (ctx: RequestContext) (online: bool) (current: LoadState) (r: InvestigationRef) : Result<LoadState * SaveEffect list, SaveFault> =
        match current, online with
        | NotLoaded, false -> Ok(UnavailableOffline, [])
        | _ -> (machine ()).StepLoad ctx current (LoadInput.OpenRequested r)

    /// Creating an investigation in a working copy at the head the listing read (C1): the session holds its
    /// StartInvestigation command; nothing is written until the user saves.
    let createInvestigation (address: Writable.T) (head: CommitOid) (start: CommandEnvelope) (online: bool) : InvestigationSession =
        (machine ()).InitialNew address start.Investigation head start online

    /// The session of a loaded working-copy investigation (T-L03); a catalog investigation has none.
    let sessionOf (verified: VerifiedInvestigation) : InvestigationSession option =
        Writable.ofAddress verified.Address |> Option.map (fun w -> (machine ()).Initial w verified)

    /// A save request (T-S04): the next session and the createCommitOnBranch to send.
    let save (ctx: RequestContext) (session: InvestigationSession) =
        (machine ()).Step ctx session SaveInput.SaveRequested
