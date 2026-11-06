// Owner: ui. The Investigations destination's frame (UI-020): its heading and what it can honestly say before the
// connection and listing slices fill it in. Keys start with "investigations.".
namespace Causa.App.Views

open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module InvestigationList =
    let view (state: AppState) : (string * ViewValue) list =
        let connected = match state.Model.Connection with Connected _ -> true | _ -> false
        [ Bind.text "investigations.heading" "Investigations"
          Bind.flag "investigations.connected" connected
          Bind.text "investigations.status" (if connected then "" else "Connect a data repository to list its investigations.") ]
