// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-009 (live): concurrency fixtures. Requests are sent to the provider at once and their answers delivered to SM-LOAD
// in a deterministic, chosen order (delayed, reordered, superseded, cancelled, retried).
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF009Tests =
    /// A sent request and the answer the provider gave it, held until the scenario delivers it.
    type private Held = { Request: HttpRequest; Outcome: HttpOutcome }

    let private send (p: GitHubProvider) (request: HttpRequest) = { Request = request; Outcome = p.Execute request }

    let private step (m: SaveMachine) (p: GitHubProvider) (state: LoadState) (input: LoadInput) =
        m.StepLoad (ProviderPort.context p) state input |> Result.defaultWith (fun f -> failwithf "SM-LOAD refused: %s" f.Reason)

    /// Interprets one answer, executing at once any follow-up read of the same load (the blob of a large file, the
    /// catalog manifest a catalog case is checked against): the state the load is in and the event the answer means.
    let rec private settle (m: SaveMachine) (p: GitHubProvider) (state: LoadState) (outcome: HttpOutcome) : LoadState * LoadInput =
        match m.InterpretLoad (ProviderPort.context p) state outcome with
        | LoadNextRequest(next, request) -> settle m p next (p.Execute request)
        | LoadEvent input -> state, input

    /// Delivers one held answer: interpret, then step the event. A superseded answer leaves the state as it is: a
    /// no-op where the machine defines one, and the fail-closed fault of an illegal pair (state unchanged) elsewhere.
    let private deliver (m: SaveMachine) (p: GitHubProvider) (state: LoadState) (held: HttpOutcome) : LoadState =
        match settle m p state held with
        | at, (LoadInput.SupersededReply _ as input) ->
            match m.StepLoad (ProviderPort.context p) at input with
            | Ok(next, effects) ->
                Assert.Empty(effects)
                next
            | Error fault ->
                Assert.Equal("SupersededReply", fault.Input)
                at
        | at, input -> step m p at input |> fst

    let private readAt (s: LoadState) =
        match s with
        | Loaded v -> Some v.ReadAt
        | _ -> None

    let private target (p: GitHubProvider) =
        FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        (ProviderPort.list p ActiveCatalog |> Result.defaultWith (fun f -> failwith (ProviderPort.failureCode f))).Investigations.Head.Ref

    /// Request A is sent and its answer delayed; the host cancels it (its outcome is lost); the retry B is sent after
    /// another writer moved the branch. Whichever order the two answers arrive in, the state is B's: the newer head.
    let private supersede (p: GitHubProvider) (newerFirst: bool) =
        let r = target p
        let m = SaveMachines.create SaveMachines.support
        let s1, e1 = step m p NotLoaded (LoadInput.OpenRequested r)
        let a = send p (SaveKit.requests e1).Head
        let s2 = deliver m p s1 (NotAnswered(a.Request.Id, LostAfterSending))
        Assert.Equal(LoadFailed(ProviderFailed(NetworkUnavailable LostAfterSending)), s2)
        let newer = FixtureData.seed p [ "workspaces/demo/causa-app-pf009/other.json", "{}\n"B ] "another writer"
        let s3, e3 = step m p s2 (LoadInput.OpenRequested r)
        let b = send p (SaveKit.requests e3).Head
        Assert.NotEqual(a.Request.Id, b.Request.Id)
        let final =
            if newerFirst then deliver m p (deliver m p s3 b.Outcome) a.Outcome
            else deliver m p (deliver m p s3 a.Outcome) b.Outcome
        Assert.Equal(Some newer, readAt final)
        // A's real answer was a verified read at the older head: it was never applied.
        match settle m p (Loading(a.Request.Id, LoadHead)) a.Outcome with
        | _, LoadInput.LoadVerified older -> Assert.NotEqual(newer, older.ReadAt)
        | _, other -> failwithf "A's answer was not a verified read: %A" other
        final

    /// O1: delayed, reordered, superseded, cancelled and retried requests are scheduled deterministically, in both
    /// completion orders, with the same final state.
    let o1 (mk: string -> GitHubProvider) =
        let real = mk "pf009-o1"
        let fixture, _ = FixtureData.fixtureProvider []
        for p in [ fixture; real ] do
            let one = supersede p true
            let two = supersede p false
            Assert.Equal(readAt one |> Option.isSome, true)
            Assert.Equal(readAt two |> Option.isSome, true)

    /// O2: a stale answer never overwrites the state of a newer request, and a cached success never masks a new
    /// authentication failure: after the credential changes, the refresh is refused, and the earlier success is not
    /// shown in its place.
    let o2 (mk: string -> GitHubProvider) =
        let real = mk "pf009-o2"
        // Stale after newer (Loaded): a late answer to a superseded request leaves the newer version.
        let r = target real
        let m = SaveMachines.create SaveMachines.support
        let s1, e1 = step m real NotLoaded (LoadInput.OpenRequested r)
        let a = send real (SaveKit.requests e1).Head
        let loaded = deliver m real s1 a.Outcome
        let s2, e2 = step m real loaded LoadInput.RefreshRequested
        let b = send real (SaveKit.requests e2).Head
        let refreshed = deliver m real s2 b.Outcome
        Assert.Equal(refreshed, deliver m real refreshed a.Outcome)
        // A new credential generation: the refresh carries the new credential and GitHub refuses it.
        let revoked = { real with Connection = { real.Connection with Credential = Credential.create "revoked-credential-for-conformance" } }
        let s3, e3 = step m revoked refreshed LoadInput.RefreshRequested
        let c = send revoked (SaveKit.requests e3).Head
        Assert.Contains(("Authorization", "Bearer revoked-credential-for-conformance"), c.Request.Headers)
        let after = deliver m revoked s3 c.Outcome
        Assert.Equal(LoadFailed(ProviderFailed AuthenticationFailed), after)
        // The earlier success, delivered late, does not replace the refusal.
        Assert.Equal(after, deliver m revoked after b.Outcome)
        // No answer can come from a cache: every read is an uncacheable POST with no conditional headers.
        for held in [ a; b; c ] do
            Assert.Equal(Post, held.Request.Method)
            Assert.DoesNotContain(held.Request.Headers, fun (k, _) -> k.StartsWith("If-", StringComparison.OrdinalIgnoreCase))

    /// O3: request identity, active provider, credential generation and freshness are honoured: identities are unique,
    /// an answer is applied only to the request that is outstanding, a request goes to the provider and credential
    /// active when it is made, and a verified read is at the branch head current when it was read.
    let o3 (mk: string -> GitHubProvider) =
        let real = mk "pf009-o3"
        let fixture, _ = FixtureData.fixtureProvider []
        let rReal = target real
        let rFixture = target fixture
        let m = SaveMachines.create SaveMachines.support
        // Opened from the fixture provider, then the active provider changes to GitHub.
        let s1, e1 = step m fixture NotLoaded (LoadInput.OpenRequested rFixture)
        let fromFixture = send fixture (SaveKit.requests e1).Head
        let s2 = deliver m fixture s1 (NotAnswered(fromFixture.Request.Id, TimedOut))
        let s3, e3 = step m real s2 (LoadInput.OpenRequested rReal)
        let fromReal = send real (SaveKit.requests e3).Head
        Assert.Contains(sprintf "\"owner\":\"%s\"" real.Connection.Repository.Owner, fromReal.Request.Body.Value)
        Assert.Contains(sprintf "refs/heads/%s" real.Connection.Repository.Branch, fromReal.Request.Body.Value)
        // The previous provider's late answer is not the active request's: it is superseded.
        Assert.Equal(s3, deliver m real s3 fromFixture.Outcome)
        let final = deliver m real s3 fromReal.Outcome
        let head = Scenarios.readHead real |> Probes.ok "head"
        Assert.Equal(Some head, readAt final)
        let ids = [ fromFixture.Request.Id; fromReal.Request.Id ]
        Assert.Equal(ids.Length, (List.distinct ids).Length)
        match fromReal.Outcome with
        | Answered(id, _, _, _) -> Assert.Equal(fromReal.Request.Id, id)
        | NotAnswered(id, _) -> Assert.Equal(fromReal.Request.Id, id)

    [<Fact>]
    let ``PF-009-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-009-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-009-O3`` () = o3 PfKit.live
