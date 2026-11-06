// Owner: persistence.
namespace Causa.Contract.Tests.Errors

open System
open Xunit
open Causa.Persistence

/// IF41-ERRORS consumed by persistence: every ProviderError case has a user-facing sentence stating its cause.
module ConsumerPersistence =

    [<Fact>]
    let EveryProviderErrorHasAMessage () =
        let oid = CommitOid(String('e', 40))
        let every =
            [ AuthenticationFailed
              PermissionDenied "write to the branch"
              RateLimited(Primary(Some 1793000000L))
              RateLimited(Primary None)
              RateLimited(Secondary(Some 60))
              RateLimited(Secondary None)
              StaleHead(oid, None)
              ValidationFailed [ "bad" ]
              NotFound "x"
              HistoryRewritten oid
              NetworkUnavailable FailedBeforeSending
              NetworkUnavailable LostAfterSending
              NetworkUnavailable TimedOut
              Malformed "m"
              ReadBackMismatch oid
              Unrecognised 502 ]
        let messages = every |> List.map Classify.describe
        Assert.All(messages, fun m -> Assert.False(String.IsNullOrWhiteSpace m))
        // A rate limit is never worded as a permission problem, and a network failure never as another writer's change.
        Assert.DoesNotContain("cannot", Classify.describe (RateLimited(Secondary None)))
        Assert.DoesNotContain("moved", Classify.describe (NetworkUnavailable LostAfterSending))
        Assert.DoesNotContain("moved", Classify.describe (NetworkUnavailable FailedBeforeSending))
        Assert.Contains("60 seconds", Classify.describe (RateLimited(Secondary(Some 60))))
