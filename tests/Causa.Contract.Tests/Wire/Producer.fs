// Owner: persistence.
namespace Causa.Contract.Tests.Wire

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

/// IF41-WIRE producer tests: the requests persistence builds are data that carry their identity and the credential
/// only in the Authorization header; outcomes name the request they answer.
module Producer =

    let private token = "wire-credential-" + String('Q', 20)
    let private connection = { Repository = { Owner = "o"; Name = "r"; Branch = "b" }; Credential = Credential.create token }
    let private oid = CommitOid(String('1', 40))

    let private requests () =
        let commit =
            { ExpectedHead = oid
              Headline = "h"
              Body = ""
              Marker = Some "s.1"
              Additions = [ { Path = RepoPath "w/investigations/x/events.ndjson"; Contents = "{}"B } ]
              Deletions = [] }
        [ GitHubRequests.connect (RequestId 11L) connection
          GitHubRequests.branchHead (RequestId 12L) connection
          GitHubRequests.fileAt (RequestId 13L) connection oid (RepoPath "w/investigations/x/manifest.json")
          GitHubRequests.blob (RequestId 14L) connection "abc"
          GitHubRequests.commit (RequestId 15L) connection oid
          GitHubRequests.compare (RequestId 16L) connection oid 2
          GitHubRequests.commitOnBranch (RequestId 17L) connection commit ]

    [<Fact>]
    let RequestsCarryIdentity () =
        let rs = requests ()
        Assert.Equal<RequestId list>([ for i in 11L..17L -> RequestId i ], rs |> List.map _.Id)
        // Each request names the operation it performs, so its answer can be read.
        Assert.All(rs, fun r -> Assert.True((GitHubRequests.operationOf r).IsSome, r.Url))
        Assert.Equal(Some(CommitOnBranch oid), GitHubRequests.operationOf (List.last rs))

    [<Fact>]
    let OutcomeNamesItsRequest () =
        let provider, running = Scenarios.fakeProvider "b" [ "a.json", "{}"B ]
        let request = GitHubRequests.branchHead (RequestId 41L) provider.Connection
        match provider.Execute request with
        | Answered(id, 200, _, _) -> Assert.Equal(RequestId 41L, id)
        | other -> failwithf "%A" other
        running.Update(StatefulFake.injectFault NextRequest LoseAnswerBeforeApplying)
        Assert.Equal(NotAnswered(RequestId 42L, LostAfterSending), provider.Execute { request with Id = RequestId 42L })
        match Classify.answer ReadBranchHead (provider.Execute { request with Id = RequestId 43L }) with
        | Ok answer -> Assert.Equal(RequestId 43L, answer.Request)
        | Error e -> failwithf "%A" e

    [<Fact>]
    let CredentialOnlyInAuthorizationHeader () =
        for r in requests () do
            Assert.DoesNotContain(token, r.Url)
            Assert.False(r.Body |> Option.exists (fun b -> b.Contains token))
            Assert.Equal<(string * string) list>([ "Authorization", "Bearer " + token ], r.Headers |> List.filter (fun (_, v) -> v.Contains token))
        Assert.Equal("<credential>", string connection.Credential)
