// Owner: persistence.
namespace Causa.TestSupport

open System
open Causa.Persistence

/// The GitHub answer forms the classifier and the stateful fake are built from (PF-013, P-03). Every form is sanitized:
/// no credential, no request id that names a real request, synthetic oids. Provenance says where each form comes from:
/// - "live R7": observed live against GitHub in Rehearsal 7 and recorded in github-error-matrix.json (KD-020, KD-022);
/// - "github-docs": the documented GitHub REST/GraphQL answer form;
/// - "transport": not a GitHub answer but what the host reports when no answer arrives (IF41-WIRE).
/// The live suite (PERSIST008Tests, PERSIST025Tests, INV22.ThreeSimultaneousCommits) re-observes the forms it can
/// provoke on the test repository in every live run and fails when GitHub's answer diverges from these templates.
[<RequireQualifiedAccess>]
module Recordings =

    /// Response headers GitHub sends with every API answer (the values are synthetic).
    let standardHeaders (remaining: int) : (string * string) list =
        [ "content-type", "application/json; charset=utf-8"
          "x-github-api-version-selected", "2022-11-28"
          "x-ratelimit-limit", "5000"
          "x-ratelimit-remaining", string remaining
          "x-ratelimit-reset", "1793000000"
          "x-ratelimit-used", string (5000 - remaining)
          "x-ratelimit-resource", "core" ]

    let private quote (s: string) = Text.Json.JsonSerializer.Serialize s

    // ---- Templates: the bodies GitHub sends, parameterised by what varies. The fake answers with these. ----

    let commitCreated (oid: string) =
        sprintf """{"data":{"createCommitOnBranch":{"commit":{"oid":"%s","committedDate":"2026-11-06T17:00:00Z"}}}}""" oid

    /// GH-STALE-HEAD, the STALE_DATA form of a sequential stale expectedHeadOid.
    let staleData (expected: string) =
        sprintf
            """{"data":{"createCommitOnBranch":null},"errors":[{"type":"STALE_DATA","path":["createCommitOnBranch"],"locations":[{"line":1,"column":58}],"message":%s}]}"""
            (quote (sprintf "Expected branch to point to \"%s\" but it did not. Pull and try again." expected))

    /// GH-STALE-HEAD, the FORBIDDEN moved-head form GitHub returns when the ref moved during the update (KD-022).
    let movedHeadForbidden (branch: string) (actual: string) (expected: string) =
        sprintf
            """{"data":{"createCommitOnBranch":null},"errors":[{"type":"FORBIDDEN","path":["createCommitOnBranch"],"locations":[{"line":1,"column":58}],"message":%s}]}"""
            (quote (sprintf "refs/heads/%s is at %s but expected %s" branch actual expected))

    let graphQlForbidden =
        """{"data":{"createCommitOnBranch":null},"errors":[{"type":"FORBIDDEN","path":["createCommitOnBranch"],"locations":[{"line":1,"column":58}],"message":"Resource not accessible by integration"}]}"""

    let graphQlNotFound (branch: string) =
        sprintf
            """{"data":{"createCommitOnBranch":null},"errors":[{"type":"NOT_FOUND","path":["createCommitOnBranch"],"locations":[{"line":1,"column":58}],"message":%s}]}"""
            (quote (sprintf "Could not resolve to a Ref with the name 'refs/heads/%s'." branch))

    let graphQlInputInvalid =
        """{"errors":[{"message":"Variable $input of type CreateCommitOnBranchInput! was provided invalid value for expectedHeadOid (Could not coerce value \"not-an-oid\" to GitObjectID)","locations":[{"line":1,"column":10}],"extensions":{"value":null,"problems":[{"path":["expectedHeadOid"],"explanation":"Could not coerce value \"not-an-oid\" to GitObjectID"}]}}]}"""

    let graphQlRateLimited =
        """{"errors":[{"type":"RATE_LIMITED","message":"API rate limit already exceeded for installation ID 1."}]}"""

    let badCredentials =
        """{"message":"Bad credentials","documentation_url":"https://docs.github.com/rest","status":"401"}"""

    let notFound =
        """{"message":"Not Found","documentation_url":"https://docs.github.com/rest","status":"404"}"""

    let secondaryLimitMessage =
        """{"message":"You have exceeded a secondary rate limit. Please wait a few minutes before you try again. If you reach out to GitHub Support for help, please include the request ID 0000:0000:0000000:0000000:00000000.","documentation_url":"https://docs.github.com/rest/overview/rate-limits-for-the-rest-api#about-secondary-rate-limits","status":"403"}"""

    let primaryLimitMessage =
        """{"message":"API rate limit exceeded for installation ID 1. If you reach out to GitHub Support for help, please include the request ID 0000:0000:0000000:0000000:00000000.","documentation_url":"https://docs.github.com/rest/overview/rate-limits-for-the-rest-api#about-the-primary-rate-limit","status":"403"}"""

    let resourceNotAccessible =
        """{"message":"Resource not accessible by integration","documentation_url":"https://docs.github.com/rest","status":"403"}"""

    let referenceExists =
        """{"message":"Reference already exists","documentation_url":"https://docs.github.com/rest/git/refs#create-a-reference","status":"422"}"""

    let refAnswer (branch: string) (oid: string) =
        sprintf """{"ref":"refs/heads/%s","node_id":"REF_synthetic","url":"https://api.github.com/repos/o/r/git/refs/heads/%s","object":{"sha":"%s","type":"commit","url":"https://api.github.com/repos/o/r/git/commits/%s"}}""" branch branch oid oid

    let fileAnswer (path: string) (blobSha: string) (bytes: byte[]) =
        let name = path.Split('/') |> Array.last
        sprintf
            """{"name":%s,"path":%s,"sha":"%s","size":%d,"type":"file","content":"%s","encoding":"base64"}"""
            (quote name)
            (quote path)
            blobSha
            bytes.Length
            (Convert.ToBase64String bytes)

    let compareAnswer (status: string) (aheadBy: int) (behindBy: int) (commits: (string * string * string option) list) =
        let items =
            commits
            |> List.map (fun (sha, message, parent) ->
                let parents =
                    match parent with
                    | Some p -> sprintf """[{"sha":"%s"}]""" p
                    | None -> "[]"
                sprintf """{"sha":"%s","commit":{"message":%s},"parents":%s}""" sha (quote message) parents)
            |> String.concat ","
        sprintf """{"status":"%s","ahead_by":%d,"behind_by":%d,"total_commits":%d,"commits":[%s]}""" status aheadBy behindBy aheadBy items

    let commitAnswer (sha: string) (message: string) (parent: string option) =
        let parents =
            match parent with
            | Some p -> sprintf """[{"sha":"%s"}]""" p
            | None -> "[]"
        sprintf """{"sha":"%s","commit":{"message":%s},"parents":%s}""" sha (quote message) parents

    let repositoryAnswer (owner: string) (name: string) (push: bool) =
        sprintf
            """{"name":%s,"full_name":%s,"private":true,"default_branch":"main","permissions":{"admin":false,"maintain":false,"push":%b,"triage":%b,"pull":true}}"""
            (quote name)
            (quote (owner + "/" + name))
            push
            push

    // ---- The recorded forms, each with the classification the error matrix requires. ----

    let expectedOid = String('e', 40)
    let actualOid = String('a', 40)
    let newOid = String('c', 40)

    type Expectation =
        | Accepted
        | Refused of ProviderError

    type RecordedAnswer =
        { /// A short stable name of the form.
          Form: string
          /// The github-error-matrix.json class it belongs to.
          MatrixClass: string
          Provenance: string
          Operation: GitHubOperation
          Outcome: HttpOutcome
          Expected: Expectation }

    let private rid = RequestId 1L
    let private answered status headers body = Answered(rid, status, headers, body)
    let private mutation = CommitOnBranch(CommitOid expectedOid)
    let private file = ReadFileAt(RepoPath "workspaces/demo/causa-app-r/investigations/x/manifest.json", CommitOid expectedOid)

    let all: RecordedAnswer list =
        [ { Form = "graphql-stale-data"
            MatrixClass = "GH-STALE-HEAD"
            Provenance = "github-docs; re-observed by PERSIST008Tests.PERSIST-008-O1 in every live run"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) (staleData expectedOid)
            Expected = Refused(StaleHead(CommitOid expectedOid, None)) }
          { Form = "graphql-forbidden-moved-head"
            MatrixClass = "GH-STALE-HEAD"
            Provenance = "live R7 (KD-022): FORBIDDEN 'is at <oid> but expected <oid>' under concurrent writers; re-observed by INV22.ThreeSimultaneousCommits"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) (movedHeadForbidden "causa-acceptance" actualOid expectedOid)
            Expected = Refused(StaleHead(CommitOid expectedOid, Some(CommitOid actualOid))) }
          { Form = "rest-403-secondary-retry-after"
            MatrixClass = "GH-RATE-SECONDARY"
            Provenance = "github-docs (secondary rate limits: 403 with retry-after)"
            Operation = mutation
            Outcome = answered 403 (("retry-after", "60") :: standardHeaders 4990) secondaryLimitMessage
            Expected = Refused(RateLimited(Secondary(Some 60))) }
          { Form = "rest-403-secondary-message-only"
            MatrixClass = "GH-RATE-SECONDARY"
            Provenance = "live R7 (KD-020): 403 secondary-limit message without retry-after during concurrent live writers"
            Operation = mutation
            Outcome = answered 403 (standardHeaders 4990) secondaryLimitMessage
            Expected = Refused(RateLimited(Secondary None)) }
          { Form = "rest-429-secondary"
            MatrixClass = "GH-RATE-SECONDARY"
            Provenance = "github-docs (secondary rate limits may answer 429)"
            Operation = file
            Outcome = answered 429 (("retry-after", "30") :: standardHeaders 4990) secondaryLimitMessage
            Expected = Refused(RateLimited(Secondary(Some 30))) }
          { Form = "graphql-rate-limited-remaining-nonzero"
            MatrixClass = "GH-RATE-SECONDARY"
            Provenance = "github-docs (GraphQL RATE_LIMITED error type)"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 120) graphQlRateLimited
            Expected = Refused(RateLimited(Secondary None)) }
          { Form = "rest-403-primary"
            MatrixClass = "GH-RATE-PRIMARY"
            Provenance = "github-docs (primary rate limit: x-ratelimit-remaining 0)"
            Operation = ReadBranchHead
            Outcome = answered 403 (standardHeaders 0) primaryLimitMessage
            Expected = Refused(RateLimited(Primary(Some 1793000000L))) }
          { Form = "rest-429-primary"
            MatrixClass = "GH-RATE-PRIMARY"
            Provenance = "github-docs"
            Operation = file
            Outcome = answered 429 (standardHeaders 0) primaryLimitMessage
            Expected = Refused(RateLimited(Primary(Some 1793000000L))) }
          { Form = "graphql-rate-limited-remaining-zero"
            MatrixClass = "GH-RATE-PRIMARY"
            Provenance = "github-docs (GraphQL primary limit answers 200 with RATE_LIMITED)"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 0) graphQlRateLimited
            Expected = Refused(RateLimited(Primary(Some 1793000000L))) }
          { Form = "rest-401-bad-credentials"
            MatrixClass = "GH-AUTH"
            Provenance = "github-docs; re-observed by PERSIST008Tests.PERSIST-008-O2 in every live run"
            Operation = ConnectRepository
            Outcome = answered 401 [ "content-type", "application/json; charset=utf-8" ] badCredentials
            Expected = Refused AuthenticationFailed }
          { Form = "graphql-401-bad-credentials"
            MatrixClass = "GH-AUTH"
            Provenance = "github-docs; re-observed by PERSIST008Tests.PERSIST-008-O2 in every live run"
            Operation = mutation
            Outcome = answered 401 [ "content-type", "application/json; charset=utf-8" ] badCredentials
            Expected = Refused AuthenticationFailed }
          { Form = "graphql-forbidden-not-accessible"
            MatrixClass = "GH-PERMISSION"
            Provenance = "github-docs (an installation credential without contents write)"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) graphQlForbidden
            Expected = Refused(PermissionDenied "write to the branch") }
          { Form = "rest-403-not-accessible"
            MatrixClass = "GH-PERMISSION"
            Provenance = "github-docs (403 with x-ratelimit-remaining not 0 and no limit message)"
            Operation = CreateBranch
            Outcome = answered 403 (standardHeaders 4990) resourceNotAccessible
            Expected = Refused(PermissionDenied "create a branch") }
          { Form = "rest-404-connect-hidden-repository"
            MatrixClass = "GH-PERMISSION"
            Provenance = "github-docs (GitHub hides repositories the credential cannot read)"
            Operation = ConnectRepository
            Outcome = answered 404 (standardHeaders 4990) notFound
            Expected = Refused(PermissionDenied "read the repository") }
          { Form = "rest-422-reference-exists"
            MatrixClass = "GH-VALIDATION"
            Provenance = "github-docs; re-observed by PERSIST008Tests.PERSIST-008-O2 in every live run"
            Operation = CreateBranch
            Outcome = answered 422 (standardHeaders 4990) referenceExists
            Expected = Refused(ValidationFailed [ "Reference already exists" ]) }
          { Form = "graphql-input-coercion"
            MatrixClass = "GH-VALIDATION"
            Provenance = "github-docs (GraphQL variable validation with extensions.problems)"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) graphQlInputInvalid
            Expected =
              Refused(
                  ValidationFailed
                      [ "Variable $input of type CreateCommitOnBranchInput! was provided invalid value for expectedHeadOid (Could not coerce value \"not-an-oid\" to GitObjectID)" ]
              ) }
          { Form = "rest-404-file-at-commit"
            MatrixClass = "GH-NOT-FOUND"
            Provenance = "github-docs; re-observed by PERSIST008Tests.PERSIST-008-O2 in every live run"
            Operation = file
            Outcome = answered 404 (standardHeaders 4990) notFound
            Expected = Refused(NotFound(sprintf "workspaces/demo/causa-app-r/investigations/x/manifest.json at %s" expectedOid)) }
          { Form = "graphql-not-found-branch"
            MatrixClass = "GH-NOT-FOUND"
            Provenance = "github-docs (GraphQL NOT_FOUND)"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) (graphQlNotFound "gone")
            Expected = Refused(NotFound "branch") }
          { Form = "rest-compare-diverged"
            MatrixClass = "GH-HISTORY-REWRITTEN"
            Provenance = "github-docs (compare status diverged)"
            Operation = CompareFrom(CommitOid expectedOid)
            Outcome = answered 200 (standardHeaders 4990) (compareAnswer "diverged" 1 1 [ newOid, "other", Some actualOid ])
            Expected = Refused(HistoryRewritten(CommitOid expectedOid)) }
          { Form = "rest-compare-404"
            MatrixClass = "GH-HISTORY-REWRITTEN"
            Provenance = "github-docs (compare of an unreachable commit)"
            Operation = CompareFrom(CommitOid expectedOid)
            Outcome = answered 404 (standardHeaders 4990) notFound
            Expected = Refused(HistoryRewritten(CommitOid expectedOid)) }
          { Form = "transport-failed-before-sending"
            MatrixClass = "GH-NETWORK-BEFORE"
            Provenance = "transport"
            Operation = mutation
            Outcome = NotAnswered(rid, FailedBeforeSending)
            Expected = Refused(NetworkUnavailable FailedBeforeSending) }
          { Form = "transport-lost-after-sending"
            MatrixClass = "GH-NETWORK-AFTER"
            Provenance = "transport; provoked live by PERSIST008Tests.PERSIST-008-O3"
            Operation = mutation
            Outcome = NotAnswered(rid, LostAfterSending)
            Expected = Refused(NetworkUnavailable LostAfterSending) }
          { Form = "transport-timed-out"
            MatrixClass = "GH-NETWORK-AFTER"
            Provenance = "transport"
            Operation = ReadBranchHead
            Outcome = NotAnswered(rid, TimedOut)
            Expected = Refused(NetworkUnavailable TimedOut) }
          { Form = "rest-502-mutation"
            MatrixClass = "GH-UNKNOWN-OUTCOME"
            Provenance = "github-docs (5xx)"
            Operation = mutation
            Outcome = answered 502 [ "content-type", "text/html" ] "<html><body>502 Bad Gateway</body></html>"
            Expected = Refused(Unrecognised 502) }
          { Form = "graphql-unknown-error-type"
            MatrixClass = "GH-UNKNOWN-OUTCOME"
            Provenance = "synthetic: an error type the classifier does not recognise"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) """{"data":{"createCommitOnBranch":null},"errors":[{"type":"SOMETHING_NEW","message":"Something went wrong."}]}"""
            Expected = Refused(Unrecognised 200) }
          { Form = "graphql-200-not-json"
            MatrixClass = "GH-MALFORMED"
            Provenance = "synthetic: a 2xx body that does not decode"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) "<!DOCTYPE html>"
            Expected = Refused(Malformed "the mutation answer is not JSON") }
          { Form = "graphql-200-no-commit"
            MatrixClass = "GH-MALFORMED"
            Provenance = "synthetic: a 2xx answer lacking the commit oid"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) """{"data":{"createCommitOnBranch":null}}"""
            Expected = Refused(Malformed "createCommitOnBranch answered without a commit oid") }
          { Form = "graphql-commit-created"
            MatrixClass = "success"
            Provenance = "github-docs; re-observed by PERSIST025Tests.PERSIST-025-O5 in every live run"
            Operation = mutation
            Outcome = answered 200 (standardHeaders 4990) (commitCreated newOid)
            Expected = Accepted }
          { Form = "rest-ref"
            MatrixClass = "success"
            Provenance = "github-docs; re-observed in every live run"
            Operation = ReadBranchHead
            Outcome = answered 200 (standardHeaders 4990) (refAnswer "causa-acceptance" actualOid)
            Expected = Accepted }
          { Form = "rest-contents-file"
            MatrixClass = "success"
            Provenance = "github-docs; re-observed in every live run"
            Operation = file
            Outcome = answered 200 (standardHeaders 4990) (fileAnswer "workspaces/demo/causa-app-r/investigations/x/manifest.json" newOid [| 123uy; 125uy |])
            Expected = Accepted }
          { Form = "rest-compare-ahead"
            MatrixClass = "success"
            Provenance = "github-docs; re-observed in every live run"
            Operation = CompareFrom(CommitOid expectedOid)
            Outcome = answered 200 (standardHeaders 4990) (compareAnswer "ahead" 1 0 [ newOid, "other", Some expectedOid ])
            Expected = Accepted } ]

    /// Strings that would reveal a credential if they appeared in a recording, a log or an artifact (AR-014-O2).
    let credentialMarkers = [ "ghp_"; "gho_"; "ghu_"; "ghs_"; "ghr_"; "github_pat_"; "Bearer "; "token " ]

    /// Replaces every occurrence of the given secrets, and the value of any Authorization header, by "<redacted>".
    let sanitize (secrets: string list) (text: string) : string =
        secrets
        |> List.filter (fun s -> not (String.IsNullOrEmpty s))
        |> List.fold (fun (acc: string) s -> acc.Replace(s, "<redacted>")) text
        |> fun t -> Text.RegularExpressions.Regex.Replace(t, "(?i)(authorization\"?\\s*[:=,]\\s*\"?)(bearer|token)\\s+[^\"\\s,}]+", "$1<redacted>")
