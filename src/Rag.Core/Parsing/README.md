# Rag.Core Parsing

Document parser adapters and parser resolution.

Parser support includes one-document files (`txt`, `md`, `pdf`, `html`, `htm`) and structured multi-document files (`json`, `jsonl`, `ndjson`, `jsonl.gz`, `ndjson.gz`, `csv`). Resolution uses extension or content type and returns normalized text plus metadata.

`IDocumentParserResolver.ParseAsync` is the single entry point for both kinds: it selects the parser
that claims the path and streams the results, so callers never branch on whether a format yields one
document or many. Single-document formats yield exactly one.

All three structured formats share `StructuredRecordProjector`, which owns the per-record steps they
have in common (build text from the profile, skip on a missing required field, derive the record
key, and stamp metadata attributes). Each parser supplies only its own reading loop and how to
resolve a field key — a JSON pointer for `json`/`jsonl`, a column name for `csv`.

Structured parsers stream: each record is yielded as it is read instead of buffering the whole file,
so a multi-gigabyte JSONL corpus ingests at flat memory. One consequence is that
`structuredSkippedRecords` carries the running skipped count at the moment a record was emitted, so
the final record of a file carries that file's total.

Structured JSON/JSONL/CSV parsing requires a dataset-local schema sidecar. Discovery checks exact sidecars such as `records.json.schema.json`, `records.jsonl.schema.json`, or `records.csv.schema.json`, then directory-level `rag-ingestion.schema.json`. JSON and JSONL fields use JSON Pointer paths; CSV fields use header names. A JSON file can contain either one object or an array of objects. Text fields can declare `plain`, `markdown`, `html`, or `auto` formatting. Each record becomes a separate document with `recordIndex`, `recordKey`, `structuredFormat`, and configured metadata attributes.

Example schema:

```json
{
  "version": 1,
  "profiles": [
    {
      "files": ["*.jsonl"],
      "format": "jsonl",
      "id": "/id",
      "text": [
        { "path": "/title", "format": "plain", "label": "Title" },
        { "path": "/body", "format": "html", "label": "Body", "required": true }
      ],
      "metadata": {
        "url": "/url"
      }
    }
  ]
}
```
