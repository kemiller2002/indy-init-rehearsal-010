// Owner: integration. HELD until the owner records ODR-COMP-P13 / ODR-R10-36 (the test deploy key exception to
// EXT-DATA-004-O1 and DF-CON-2026-A002's baseline check). Until then this branch is not merged.
//
// The deployed test build's data connection, made at deploy time from four GitHub Actions secrets, and the credential
// scan of the deploy artifact. Pure: the environment is passed in as a lookup and the artifact as (path, text) pairs.
// Loaded by build.fsx (`#load`) and compiled into tests/Causa.App.Tests, so the build and its tests run the same code.
//
// The exception is exactly this, and nothing wider:
//   - one key, the secret DATA_ACCESS_KEY_CAUSA, for one repository, REPO_OWNER/DATA_SOURCE on branch CAUSA_BRANCH;
//   - only in the deploy build (`CAUSA_DEPLOY_CONNECTION=1 npm run build`, set only by .github/workflows/deploy-pages.yml);
//   - only in dist/deploy-connection.json, never in another file of the artifact, never in the repository;
//   - never printed: a finding or an error names the secret or the file, never the value.
// Every other build (local, Build and test, the Verification gate) writes a connection-less file and bundles no key.
module DeployConnection

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

/// The file the deploy build writes next to index.html, read by the application after its build record.
[<Literal>]
let FileName = "deploy-connection.json"

[<Literal>]
let Schema = "causa.deploy-connection/1"

/// The repository secrets the deploy workflow passes to the build, by what each one names.
[<Literal>]
let OwnerSecret = "REPO_OWNER"

[<Literal>]
let RepositorySecret = "DATA_SOURCE"

[<Literal>]
let BranchSecret = "CAUSA_BRANCH"

[<Literal>]
let KeySecret = "DATA_ACCESS_KEY_CAUSA"

let secretNames = [ OwnerSecret; RepositorySecret; BranchSecret; KeySecret ]

/// The designated test data repository and its key. Its printed forms never show the key.
[<CustomEquality; NoComparison; StructuredFormatDisplay("{Display}")>]
type Settings =
    { Owner: string
      Repository: string
      Branch: string
      Key: string }
    member s.Display = sprintf "%s/%s@%s (key <credential>)" s.Owner s.Repository s.Branch
    override s.ToString() = s.Display
    override s.Equals(other) =
        match other with
        | :? Settings as o -> s.Owner = o.Owner && s.Repository = o.Repository && s.Branch = o.Branch && s.Key = o.Key
        | _ -> false
    override s.GetHashCode() = hash (s.Owner, s.Repository, s.Branch)

let private ownerShape = Regex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")
let private repositoryShape = Regex(@"^[A-Za-z0-9._-]{1,100}$")
let private branchShape = Regex(@"^(?!/)(?!.*\.\.)(?!.*//)[A-Za-z0-9._/-]{1,200}(?<!/)$")
let private keyShape = Regex(@"^[\x21-\x7E]{8,}$")

/// Why a present value is unusable, naming the secret only.
let private shapeProblems (owner: string) (repository: string) (branch: string) (key: string) =
    [ if not (ownerShape.IsMatch owner) then sprintf "%s is not a GitHub owner name" OwnerSecret
      if not (repositoryShape.IsMatch repository) || repository = "." || repository = ".." then sprintf "%s is not a repository name" RepositorySecret
      if not (branchShape.IsMatch branch) then sprintf "%s is not a branch name" BranchSecret
      if not (keyShape.IsMatch key) then sprintf "%s is not a single printable key" KeySecret ]

/// The connection the deploy build injects. None when no secret is set; Some when all four are set and well formed;
/// an error naming the missing or malformed secrets (never a value) when only some are, so a half-configured deploy
/// fails instead of shipping a broken connection.
let fromLookup (lookup: string -> string option) : Result<Settings option, string> =
    let value name = lookup name |> Option.map (fun v -> v.Trim()) |> Option.filter (fun v -> v <> "")
    let present = secretNames |> List.map (fun n -> n, value n)
    match present |> List.filter (snd >> Option.isNone) |> List.map fst with
    | missing when missing.Length = secretNames.Length -> Ok None
    | [] ->
        let v name = present |> List.pick (fun (n, x) -> if n = name then x else None)
        let owner, repository, branch, key = v OwnerSecret, v RepositorySecret, v BranchSecret, v KeySecret
        match shapeProblems owner repository branch key with
        | [] -> Ok(Some { Owner = owner; Repository = repository; Branch = branch; Key = key })
        | problems -> Error(String.Join("; ", problems))
    | missing -> Error(sprintf "the deploy connection needs all four secrets; not set: %s" (String.Join(", ", missing)))

/// The text of deploy-connection.json: the schema alone without a connection, or the schema and the connection.
let render (settings: Settings option) : string =
    use stream = new MemoryStream()
    do
        use w = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        w.WriteStartObject()
        w.WriteString("schema", Schema)
        match settings with
        | Some s ->
            w.WriteStartObject("connection")
            w.WriteString("owner", s.Owner)
            w.WriteString("repository", s.Repository)
            w.WriteString("branch", s.Branch)
            w.WriteString("key", s.Key)
            w.WriteEndObject()
        | None -> ()
        w.WriteEndObject()
    Encoding.UTF8.GetString(stream.ToArray())

// ---- the credential scan --------------------------------------------------------------------------------------------

/// One credential found where it must not be: the file and why, never the value.
type Finding = { Path: string; Reason: string }

/// GitHub token literals (classic, OAuth, user-to-server, server-to-server, refresh, fine-grained).
let tokenPattern = Regex(@"gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}")

let private occurrences (needle: string) (text: string) =
    let rec count (from: int) (n: int) =
        match text.IndexOf(needle, from, StringComparison.Ordinal) with
        | -1 -> n
        | i -> count (i + needle.Length) (n + 1)
    if needle = "" then 0 else count 0 0

/// The connection a deploy-connection.json text holds: (owner, repository, branch, key) when it has one.
let private connectionIn (text: string) =
    try
        use doc = JsonDocument.Parse text
        match doc.RootElement.TryGetProperty "connection" with
        | true, c when c.ValueKind = JsonValueKind.Object ->
            let field (n: string) =
                match c.TryGetProperty n with
                | true, v when v.ValueKind = JsonValueKind.String -> v.GetString()
                | _ -> null
            Ok(Some(field "owner", field "repository", field "branch", field "key"))
        | _ -> Ok None
    with e -> Error e.Message

/// The scan of what ships (the deploy artifact, or any tree): every GitHub token literal is a finding, except the one
/// injected key, and that only in deploy-connection.json at the artifact root, once, for the designated repository.
/// `injected` is the deploy build's settings (None for every other build: then deploy-connection.json must hold no
/// connection at all). `files` are (path relative to the artifact root, text) pairs.
let scan (injected: Settings option) (files: (string * string) list) : Finding list =
    let key = injected |> Option.map (fun s -> s.Key)
    let inFile (path: string, text: string) =
        let isConfig = path.Replace('\\', '/') = FileName
        let literals =
            tokenPattern.Matches text
            |> Seq.map (fun m -> m.Value)
            |> Seq.filter (fun t -> not (isConfig && Some t = key))
            |> Seq.map (fun _ -> { Path = path; Reason = "a GitHub token literal" })
            |> List.ofSeq
        let keyElsewhere =
            match key with
            | Some k when not isConfig && occurrences k text > 0 -> [ { Path = path; Reason = sprintf "the %s value outside %s" KeySecret FileName } ]
            | _ -> []
        let config =
            if not isConfig then []
            else
                match connectionIn text, injected with
                | Error why, _ -> [ { Path = path; Reason = "not valid JSON: " + why } ]
                | Ok None, _ -> []
                | Ok(Some _), None -> [ { Path = path; Reason = "a connection in a build that has no deploy secrets" } ]
                | Ok(Some(o, r, b, k)), Some s ->
                    [ if (o, r, b) <> (s.Owner, s.Repository, s.Branch) then
                          { Path = path; Reason = sprintf "a connection to another repository than %s/%s@%s" OwnerSecret RepositorySecret BranchSecret }
                      if k <> s.Key then { Path = path; Reason = sprintf "a key that is not the %s value" KeySecret }
                      if occurrences s.Key text <> 1 then { Path = path; Reason = sprintf "the %s value more than once" KeySecret } ]
        literals @ keyElsewhere @ config
    files |> List.collect inFile

/// Every file under `root` as (relative path, text). Binary files are read as UTF-8 too: a string literal survives.
let filesUnder (root: string) : (string * string) list =
    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
    |> Seq.sort
    |> Seq.map (fun f -> Path.GetRelativePath(root, f).Replace('\\', '/'), (try File.ReadAllText f with _ -> ""))
    |> List.ofSeq
