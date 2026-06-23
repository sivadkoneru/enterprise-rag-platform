# Rag.Core Vector

Vector store contracts and adapters.

Elasticsearch is the first selected adapter boundary using dense-vector mapping metadata and cosine kNN semantics. Azure AI Search is out of scope except for a future-facing interface/stub.

Search accepts optional metadata filters:

- `documentIds` limits results to selected documents.
- `sources` limits results to exact source URIs or paths.
- `origins` limits results to `file`, `s3`, or `azureblob`.
- `fileTypes` limits results to extensions such as `.txt`, `.md`, or `.pdf`.

Filter fields are combined with AND; the values inside one field are combined with OR. A value can
therefore never satisfy two fields at once, so callers must map each user-facing flag to exactly one
field. File types are normalized to lower case with a leading dot on both write and read.

The in-memory adapter filters before scoring. Elasticsearch places exact metadata filters inside the
kNN `filter` clause, indexes chunks with a single `_bulk` request per call, and treats a concurrent
`resource_already_exists_exception` during index creation as success. Set `ELASTICSEARCH_USERNAME`
and `ELASTICSEARCH_PASSWORD` to reach a secured cluster over HTTP basic authentication.
