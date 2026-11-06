// Owner: ui-3. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.App

open Causa.Domain

/// IF41-REPORT: Boundary E, the read-only report projection Folio renders.
type ReportSection = { Heading: string; Paragraphs: string list; Provenance: EventId list }

type ReportProjection =
    { Investigation: InvestigationId
      Version: AggregateVersion
      Sections: ReportSection list
      UnresolvedUncertainties: string list }

type Reporting = { Project: InvestigationProjection -> ReportProjection }
