# Rag.Core Parsing

Document parser adapters and parser resolution.

Parser support includes one-document files (`txt`, `md`, `pdf`, `html`, `htm`) and structured multi-document files (`json`, `jsonl`, `ndjson`, `jsonl.gz`, `ndjson.gz`, `csv`). Resolution uses extension or content type and returns normalized text plus metadata.

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
