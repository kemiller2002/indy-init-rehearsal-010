// Owner: ui-2. investigation-start (screen "new", route #/new; S01): the fields and actions of INV-001, INV-002,
// INV-004, INV-005, INV-006, INV-022 and INV-023, with the validation rules INV-019, INV-020 and INV-021 (every failed
// rule shown with its message). INV-003 is not applicable: the governed fact domain-profile-selected is false.
// The view validates input (Data Editable) and shows the engine's outcome; it never builds a domain command and never
// decides whether a start is allowed (UI-004): the engine turns the `new.start` event into CreateRequested.
namespace Causa.App.Views

open System
open System.Text.RegularExpressions
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Start =
    // ---- event names (data-event) and the drafts they become -------------------------------------------------------
    [<Literal>]
    let ProblemEvent = "new.problem"
    [<Literal>]
    let TitleEvent = "new.title"
    [<Literal>]
    let QuestionEvent = "new.question"
    /// The identifier field. Integration seeds this draft with a new investigation id generated at the host boundary.
    [<Literal>]
    let IdentifierEvent = "new.identifier"
    /// Start investigation (INV-004): an action, mapped by integration to CreateRequested.
    [<Literal>]
    let StartEvent = "new.start"
    /// Cancel investigation start (INV-022): an action, mapped by integration to Navigate #/investigations.
    [<Literal>]
    let CancelEvent = "new.cancel"
    /// Set by integration (to any non-empty text) when a start was refused by validation, so every error shows.
    [<Literal>]
    let AttemptedKey = "new.attempted"

    let guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"

    // ---- the catalog's fields and rules ------------------------------------------------------------------------------
    let problemSpec: FieldSpec =
        { Field = FieldId "INV-001"; Label = "Problem statement"; Component = TextArea; Rules = [ MinLength 1; MaxLength 10000 ] }

    let titleSpec: FieldSpec =
        { Field = FieldId "INV-002"; Label = "Title"; Component = TextInput; Rules = [ MaxLength 200 ] }

    let questionSpec: FieldSpec =
        { Field = FieldId "INV-023"; Label = "Primary investigative question"; Component = TextArea; Rules = [ MaxLength 10000 ] }

    let identifierSpec: FieldSpec =
        { Field = FieldId "INV-006"; Label = "Investigation identifier"; Component = TextInput; Rules = [ Required; Pattern guidPattern ] }

    /// Each field with the event (draft) name it is entered through.
    let fields: (FieldSpec * string) list =
        [ problemSpec, ProblemEvent; titleSpec, TitleEvent; questionSpec, QuestionEvent; identifierSpec, IdentifierEvent ]

    /// The message of one failed rule (catalog names: INV-019 "Problem statement must not be empty", INV-020 "Title
    /// length", INV-021 "Problem statement length").
    let message (spec: FieldSpec) (rule: Rule) : string =
        match rule with
        | Required -> $"Enter the {spec.Label.ToLowerInvariant()}."
        | MinLength 1 -> $"{spec.Label} must not be empty."
        | MinLength n -> $"{spec.Label} must be at least {n} characters."
        | MaxLength n -> $"{spec.Label} must be at most {n:N0} characters."
        | Pattern _ when spec.Field = FieldId "INV-006" -> "Investigation identifier must be a UUID such as 3f2504e0-4f89-41d3-9a0c-0305e82c3301."
        | Pattern _ -> $"{spec.Label} is not in the expected format."
        | OneOf options -> $"""{spec.Label} must be one of: {String.Join(", ", options)}."""

    let private fails (rule: Rule) (value: string) : bool =
        match rule with
        | Required -> String.IsNullOrWhiteSpace value
        | MinLength n -> value.Trim().Length < n
        | MaxLength n -> value.Length > n
        | Pattern p -> not (Regex.IsMatch(value, p))
        | OneOf options -> not (List.contains value options)

    /// Every rule the value fails, each with its message (not only the first).
    let validate (spec: FieldSpec) (value: string) : ValidationError list =
        spec.Rules
        |> List.filter (fun rule -> fails rule value)
        |> List.map (fun rule -> { Field = spec.Field; Rule = rule; Message = message spec rule })

    /// The form as entered (drafts).
    let form (state: AppState) : StartForm =
        { ProblemStatement = Drafts.get state ProblemEvent
          Title = Drafts.get state TitleEvent
          Question = Drafts.get state QuestionEvent
          Identifier = Drafts.get state IdentifierEvent }

    let private valueOf (f: StartForm) (spec: FieldSpec) =
        match spec.Field with
        | FieldId "INV-001" -> f.ProblemStatement
        | FieldId "INV-002" -> f.Title
        | FieldId "INV-023" -> f.Question
        | _ -> f.Identifier

    /// Every failed rule of the whole form. Integration sends CreateRequested only when this is empty.
    let validateForm (f: StartForm) : ValidationError list =
        fields |> List.collect (fun (spec, _) -> validate spec (valueOf f spec))

    /// The investigation's title: the entered title, or the problem statement when none is entered (INV-002 is
    /// optional; causa-storage requires a non-blank title, FIELD-MAPPING: the original text is the fallback).
    let titleOrFallback (f: StartForm) : string =
        if String.IsNullOrWhiteSpace f.Title then f.ProblemStatement.Trim() else f.Title.Trim()

    /// The primary question, when one was entered.
    let question (f: StartForm) : string option =
        if String.IsNullOrWhiteSpace f.Question then None else Some(f.Question.Trim())

    /// The working copy the engine listed for this repository (where a Start is written; EXT-DATA-006).
    let workingCopy (model: Model) : WorkspaceAddress.T option =
        model.Listings |> Map.toList |> List.map fst |> List.tryFind (fun a -> a.Role = WorkspaceRole.WorkingCopy)

    /// INV-005: where a Start will be written, shown before any write.
    let destination (model: Model) : Destination option =
        match model.Context, model.Connection with
        | Some context, Connected canWrite ->
            let r = context.Repository
            let (Workspace workspace) =
                workingCopy model |> Option.map (fun a -> a.Workspace) |> Option.defaultValue (Workspace "")
            Some
                { Repository = $"{r.Owner}/{r.Name}" + (if workspace = "" then "" else " · " + workspace)
                  Branch = r.Branch
                  Writable = canWrite && workspace <> "" }
        | _ -> None

    /// Whether Start is offered, from the engine's connection state (UI-004: the engine's facts, with its reasons).
    let startAvailability (model: Model) : Availability =
        match model.Connection, workingCopy model with
        | Connected true, Some _ -> Available
        | Connected true, None -> Unavailable "The working copy for new investigations has not been listed yet."
        | Connected false, _ -> Unavailable "This connection is read-only. Connect with write access to start an investigation."
        | Connecting _, _ -> Unavailable "Connecting to the data repository…"
        | Disconnected, _
        | CredentialRefused, _
        | Reauthenticating, _ -> Unavailable "Connect a data repository before starting an investigation."

    let private errorRows (errors: ValidationError list) =
        errors
        |> List.mapi (fun i e ->
            let (FieldId f) = e.Field
            [ Cell.text "id" $"{f}-{i}"; Cell.text "message" e.Message ])

    /// The screen's keys (screen "new"). Errors show for a field once it was edited, and for every field after a
    /// refused start.
    let view (state: AppState) : (string * ViewValue) list =
        let f = form state
        let attempted = Drafts.get state AttemptedKey <> ""
        let touched name = state.Drafts.ContainsKey name
        let shown =
            fields
            |> List.map (fun (spec, name) ->
                name, (if attempted || touched name then validate spec (valueOf f spec) else []))
        let allShown = shown |> List.collect snd
        let availability = startAvailability state.Model
        let destination = destination state.Model
        [ Bind.text "new.heading" "New investigation"
          for (spec, name) in fields do
              let errors = shown |> List.find (fst >> (=) name) |> snd
              Bind.text name (valueOf f spec)
              Bind.text (name + ".invalid") (if errors.IsEmpty then "false" else "true")
              Bind.flag (name + ".hasErrors") (not errors.IsEmpty)
              Bind.items (name + ".errors") (errorRows errors)
          Bind.flag "new.refused" (attempted && not allShown.IsEmpty)
          Bind.text "new.refusal" (
              match allShown.Length with
              | 0 -> ""
              | 1 -> "The investigation was not started. 1 field needs attention."
              | n -> $"The investigation was not started. {n} fields need attention.")
          Bind.items "new.summary" (errorRows allShown)
          Bind.flag "new.start.disabled" (availability <> Available)
          Bind.text "new.start.reason" (
              match availability with
              | Unavailable reason -> reason
              | Available
              | NotOffered -> "")
          Bind.flag "new.start.hasReason" (match availability with Unavailable _ -> true | _ -> false)
          Bind.text "new.destination" (
              match destination with
              | Some d when d.Writable -> $"GitHub: {d.Repository} on branch {d.Branch}"
              | Some d -> $"GitHub: {d.Repository} on branch {d.Branch} (not writable)"
              | None -> "Not connected: nothing will be written until a data repository is connected.")
          Bind.flag "new.destination.writable" (destination |> Option.exists (fun d -> d.Writable)) ]
