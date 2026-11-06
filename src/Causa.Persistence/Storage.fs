// Owner: persistence. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.Persistence

open Causa.Domain

/// IF41-STORAGE: causa-storage v1.0.0 file sets (CONTRACT.md wins over docs/CAUSA-DATA-CONTRACT-v1.md).
type CommitOid = CommitOid of string
type RepoPath = RepoPath of string

/// v4.1.5 W1: a causa-storage workspace, the directory of the connected repository that holds `investigations/<id>/`
/// (for example `workspaces/demo/r10`). It is derived, never typed: the catalog workspace is the one
/// `catalog/active.json` names (EXT-DATA-013); a working copy is `workspaces/demo/<run>` (CONTRACT section 9.4).
type Workspace = Workspace of string

/// v4.1.5 W1: what a workspace may be used for. A catalog is read-only (EXT-DATA-013); only a working copy is written
/// (CONTRACT section 9.4; EXT-DATA-006). Both are in the repository the user connected to (EXT-DATA-003).
[<RequireQualifiedAccess>]
type WorkspaceRole =
    | Catalog
    | WorkingCopy

/// v4.1.5 W1: a workspace of the connected repository, with its role. The role is derived, never asserted: the
/// representation is private, a catalog address comes only from WorkspaceAddress.catalog and a working copy only from
/// WorkspaceAddress.workingCopy (EXT-DATA-013; EXT-DATA-006).
[<RequireQualifiedAccess>]
module WorkspaceAddress =
    type T =
        private
            { Dir: Workspace
              Kind: WorkspaceRole }
        member a.Workspace = a.Dir
        member a.Role = a.Kind

    let catalog (workspace: Workspace) : T = { Dir = workspace; Kind = WorkspaceRole.Catalog }

    /// The working copy `workspaces/demo/causa-app-<run>` (CONTRACT section 3, runId ^[a-z0-9][a-z0-9-]{0,63}$) for a
    /// run matching ^[a-z0-9][a-z0-9-]{0,53}$. The reserved `causa-app-` namespace keeps every working copy apart from
    /// the catalog workspaces (demonstration-v1, historical-v1, ...), and any workspace in `catalogs` is refused as well.
    let workingCopy (catalogs: Workspace list) (run: string) : T option =
        let workspace = Workspace("workspaces/demo/causa-app-" + run)
        if System.Text.RegularExpressions.Regex.IsMatch(run, "^[a-z0-9][a-z0-9-]{0,53}\\z") && not (List.contains workspace catalogs) then
            Some { Dir = workspace; Kind = WorkspaceRole.WorkingCopy }
        else
            None

/// v4.1.5 W1: an investigation is identified by where it is and its id: the same case can be stored in two
/// workspaces (causa-data holds identical cases in historical-v1 and demonstration-v1).
type InvestigationRef = { Address: WorkspaceAddress.T; Id: InvestigationId }

/// v4.1.5 W1: an address that may be written. Its only constructor refuses a Catalog, so no catalog investigation can
/// reach a save session (EXT-DATA-013; EXT-DATA-006).
[<RequireQualifiedAccess>]
module Writable =
    type T =
        private
        | Writable of WorkspaceAddress.T

    let ofAddress (address: WorkspaceAddress.T) : T option =
        match address.Role with
        | WorkspaceRole.WorkingCopy -> Some(Writable address)
        | WorkspaceRole.Catalog -> None

    let address (Writable address) : WorkspaceAddress.T = address

type FileSet =
    { Manifest: byte[]
      Events: byte[]
      Snapshot: byte[] }

type VerificationFailure =
    | SchemaInvalid of file: string * detail: string
    | DigestMismatch of field: string
    | ContractValueMismatch of field: string * expected: string * actual: string
    | SnapshotDiffersFromReplay of paths: string list
    | MissingRequiredField of file: string * field: string
    | ReplayRejected of ReplayError
    /// v4.1.5 E1: a CONTRACT section 11.1 reader stage refused the file set; code is the stage's primary code, so
    /// conforming readers agree on it.
    | ReaderStage of code: string * file: string * line: int option * detail: string

type VerifiedInvestigation =
    { Investigation: Investigation
      History: EventEnvelope list
      ReadAt: CommitOid
      /// v4.1.5 W1: the workspace the history was read from.
      Address: WorkspaceAddress.T
      Bytes: FileSet }

type StorageCodec =
    { /// v4.1.5 W1: `<workspace>/investigations/<id>`.
      PathOf: Workspace -> InvestigationId -> RepoPath
      Encode: VerifiedInvestigation -> EventEnvelope list -> FileSet
      /// v4.1.5 C1: the first file set of a new investigation from its command-created history (EXT-DATA-008). It is
      /// Ok only when Verify accepts the result, so a file set that would not verify is never sent.
      EncodeNew: InvestigationId -> EventEnvelope list -> Result<FileSet, VerificationFailure list>
      Verify: WorkspaceAddress.T -> CommitOid -> FileSet -> Result<VerifiedInvestigation, VerificationFailure list> }
