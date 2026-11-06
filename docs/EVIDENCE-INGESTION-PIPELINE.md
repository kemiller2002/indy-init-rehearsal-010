# Evidence Ingestion Pipeline

Status: ambitious reusable evidence-processing profile.

Investigation quality depends on how raw material becomes structured evidence without losing provenance.

## 1. Pipeline

```text
Raw Source
  -> Capture
  -> Integrity metadata
  -> Parse/extract
  -> Candidate observations/findings
  -> AI/local interpretation proposals
  -> Human/policy admission
  -> Evidence relationships
```

No extraction step rewrites the raw source.

## 2. Source types

Support extensibly:

- typed text
- pasted logs
- URL
- file
- screenshot/photo
- structured JSON/CSV
- test result
- GitHub commit/diff
- Aegis fault
- Praxis metric artifact
- ROS work record
- external integration result

## 3. Capture record

Record:

- CaptureId
- source type
- captured by
- time
- source URI/location
- content digest
- media type
- size
- classification
- scope
- availability

## 4. Parse record

Each transformation records:

- parser/tool version
- input digest
- output digest
- warnings/errors
- fields/chunks extracted
- lossy/non-lossy flag

## 5. Extraction proposals

AI/local parser may propose:

- Observation
- Finding
- Evidence metadata
- timestamp
- entity/system/version
- candidate relationship

No proposal silently changes raw capture.

## 6. Screenshot/photo evidence

Pipeline may:

- preserve original image artifact
- hash it
- extract text/metadata
- propose observations

Extracted text is DerivedFrom the image and should not replace it.

## 7. Log ingestion

For large logs:

- preserve source artifact/ref
- select time range
- local prefilter
- extract lines/events
- keep line/time offsets
- generate findings with source anchors

## 8. Git diff ingestion

Capture:

- base commit
- target commit
- files
- hunks
- diff digest

Local/AI analysis may propose hypotheses/observations.

## 9. Structured test-result ingestion

JUnit/other test results can produce:

- observations
- findings
- regression evidence
- experiment results

Parser version and source build recorded.

## 10. Deduplication

Detect exact duplicates by digest.

Suggest near duplicates by similarity.

Do not silently merge semantically separate captures.

## 11. Evidence chain

Users can drill:

```text
Evidence E
  <- Finding F
  <- parsed chunk C
  <- Raw artifact A
  <- source system S
```

## 12. Classification and redaction

Before AI/external sharing:

- enforce classification
- redact/omit where policy requires
- preserve local raw source if allowed
- record transformation provenance

## 13. Ingestion failure

Malformed parser output:

- does not destroy raw artifact
- records fault
- allows alternate parser/manual capture
- no invented data

## 14. Competition use

Drop/paste a real log or Git diff.

Within seconds show:

- artifact hashed
- relevant changes extracted locally
- candidate observations
- hypothesis impact proposals
- provenance drill-down

This is a strong real-world demonstration.
