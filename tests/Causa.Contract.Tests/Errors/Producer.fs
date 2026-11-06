// Owner: persistence.
namespace Causa.Contract.Tests.Errors

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

/// IF41-ERRORS producer tests: Classify against github-error-matrix.json and its recorded answer forms.
module Producer =

    let private matrixClasses =
        [ "GH-AUTH"; "GH-PERMISSION"; "GH-RATE-PRIMARY"; "GH-RATE-SECONDARY"; "GH-STALE-HEAD"; "GH-VALIDATION"; "GH-NOT-FOUND"
          "GH-HISTORY-REWRITTEN"; "GH-NETWORK-BEFORE"; "GH-NETWORK-AFTER"; "GH-UNKNOWN-OUTCOME"; "GH-MALFORMED" ]

    [<Fact>]
    let EveryMatrixRowClassifiesItsRecordedAnswer () =
        // Every error class of the matrix that has a typed error has at least one recorded form...
        for cls in matrixClasses do
            Assert.True(Recordings.all |> List.exists (fun r -> r.MatrixClass = cls), sprintf "no recorded form for %s" cls)
        // ...and each recorded form classifies exactly as the matrix requires.
        for r in Recordings.all do
            let got = Classify.answer r.Operation r.Outcome
            match r.Expected, got with
            | Recordings.Accepted, Ok answer ->
                let id =
                    match r.Outcome with
                    | Answered(id, _, _, _) -> id
                    | NotAnswered(id, _) -> id
                Assert.Equal(id, answer.Request)
            | Recordings.Refused expected, Error actual -> Assert.True((expected = actual), sprintf "%s: expected %A, got %A" r.Form expected actual)
            | _ -> failwithf "%s: expected %A, got %A" r.Form r.Expected got

    [<Fact>]
    let SharedStatusesNeedOtherEvidence () =
        let oid = CommitOid(String('e', 40))
        let h remaining extra = extra @ Recordings.standardHeaders remaining
        let at status headers body operation = Classify.answer operation (Answered(RequestId 9L, status, headers, body))
        // 403: rate limits (by headers or message) versus a permission refusal.
        Assert.Equal(Error(RateLimited(Primary(Some 1793000000L))), at 403 (h 0 []) Recordings.resourceNotAccessible ReadBranchHead)
        Assert.Equal(Error(RateLimited(Secondary(Some 5))), at 403 (h 10 [ "retry-after", "5" ]) Recordings.resourceNotAccessible ReadBranchHead)
        Assert.Equal(Error(RateLimited(Secondary None)), at 403 (h 10 []) Recordings.secondaryLimitMessage ReadBranchHead)
        Assert.Equal(Error(PermissionDenied "read the branch"), at 403 (h 10 []) Recordings.resourceNotAccessible ReadBranchHead)
        // 404: hidden repository at Connect, missing file, unreachable compare base.
        Assert.Equal(Error(PermissionDenied "read the repository"), at 404 (h 10 []) Recordings.notFound ConnectRepository)
        Assert.Equal(Error(NotFound "branch"), at 404 (h 10 []) Recordings.notFound ReadBranchHead)
        Assert.Equal(Error(HistoryRewritten oid), at 404 (h 10 []) Recordings.notFound (CompareFrom oid))
        // 200 GraphQL: success, both stale-head forms, permission, validation, not found, unknown, malformed.
        let m = CommitOnBranch oid
        let (CommitOid e) = oid
        let a = String('a', 40)
        Assert.True(Result.isOk (at 200 (h 10 []) (Recordings.commitCreated a) m))
        Assert.Equal(Error(StaleHead(oid, None)), at 200 (h 10 []) (Recordings.staleData e) m)
        Assert.Equal(Error(StaleHead(oid, Some(CommitOid a))), at 200 (h 10 []) (Recordings.movedHeadForbidden "b" a e) m)
        Assert.Equal(Error(PermissionDenied "write to the branch"), at 200 (h 10 []) Recordings.graphQlForbidden m)
        Assert.True((match at 200 (h 10 []) Recordings.graphQlInputInvalid m with Error(ValidationFailed _) -> true | _ -> false))
        Assert.Equal(Error(NotFound "branch"), at 200 (h 10 []) (Recordings.graphQlNotFound "b") m)
        Assert.Equal(Error(Unrecognised 200), at 200 (h 10 []) """{"errors":[{"type":"NEW","message":"x"}]}""" m)
        Assert.True((match at 200 (h 10 []) "not json" m with Error(Malformed _) -> true | _ -> false))
        // A stale head is never taken from the status: a 403 that names a moved head is still a rate limit when
        // GitHub says the limit is spent (precedence), and otherwise a stale head.
        Assert.Equal(Error(RateLimited(Primary(Some 1793000000L))), at 403 (h 0 []) (Recordings.movedHeadForbidden "b" a e) m)
        Assert.Equal(Error(StaleHead(oid, Some(CommitOid a))), at 200 (h 10 []) (Recordings.movedHeadForbidden "b" a e) m)
        // The large-file 403 is not an error: the read continues through the blobs API.
        let file = ReadFileAt(RepoPath "a/b.json", oid)
        Assert.True(Result.isOk (at 403 (h 10 []) """{"message":"This API returns blobs up to 1 MB in size.","errors":[{"resource":"Blob","field":"data","code":"too_large"}]}""" file))
        // 401 is always authentication, whatever the body says.
        Assert.Equal(Error AuthenticationFailed, at 401 [] Recordings.secondaryLimitMessage m)

    [<Fact>]
    let WriteAccessOnlyFromPermissions () =
        let connection = { Repository = { Owner = "o"; Name = "r"; Branch = "b" }; Credential = Credential.create "c" }
        let id = RequestId 3L
        let request = Classify.connector.ConnectRequest id connection
        Assert.Equal(Get, request.Method)
        Assert.Equal("https://api.github.com/repos/o/r", request.Url)
        let answer body = Answered(id, 200, Recordings.standardHeaders 10, body)
        Assert.Equal(ConnectAccepted true, Classify.connector.InterpretConnect id (answer (Recordings.repositoryAnswer "o" "r" true)))
        Assert.Equal(ConnectAccepted false, Classify.connector.InterpretConnect id (answer (Recordings.repositoryAnswer "o" "r" false)))
        // A successful read that states no permissions grants no write access.
        Assert.Equal(ConnectAccepted false, Classify.connector.InterpretConnect id (answer """{"name":"r","full_name":"o/r"}"""))
        Assert.Equal(ConnectRejected AuthenticationFailed, Classify.connector.InterpretConnect id (Answered(id, 401, [], Recordings.badCredentials)))
        Assert.Equal(ConnectSuperseded, Classify.connector.InterpretConnect id (Answered(RequestId 4L, 200, [], Recordings.repositoryAnswer "o" "r" true)))
