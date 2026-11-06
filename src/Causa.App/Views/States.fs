// Owner: ui. How a state is shown (UI-010, UI-016-O13): every status has a word, and its colour only repeats it.
namespace Causa.App.Views

open Causa.Ui.Contracts

[<RequireQualifiedAccess>]
module States =
    /// A Forma status lozenge's data-state: ok (teal), attention (amber), unknown (neutral), blocked (coral).
    type Tone =
        | Ok
        | Attention
        | Neutral
        | Blocked

    let tone (t: Tone) =
        match t with
        | Ok -> "ok"
        | Attention -> "attention"
        | Neutral -> "unknown"
        | Blocked -> "blocked"

    /// A system fact's value and status word, and its tone. Unknown and unavailable are neutral: the absence of data
    /// never borrows a success or danger colour (UI-016-O17).
    let fact (f: SystemFactStatus) : string * string * Tone =
        match f with
        | FactKnown v -> v, "Known", Ok
        | FactUnknown -> "Not known yet", "Unknown", Neutral
        | FactUnavailable -> "Not available in this build", "Unavailable", Neutral
        | FactFailed why -> why, "Failed", Blocked
