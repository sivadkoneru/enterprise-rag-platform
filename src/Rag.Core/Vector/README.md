# Rag.Core Vector

Vector store contracts and adapters.

Elasticsearch is the first selected adapter boundary using dense-vector mapping metadata and cosine kNN semantics. Azure AI Search is out of scope except for a future-facing interface/stub.

Search accepts optional metadata filters:

- `documentIds` limits results to selected documents.
- `sources` limits results to exact source URIs or paths.
- `origins` limits results to `file`, `s3`, or `azureblob`.
- `fileTypes` limits results to extensions such as `.txt`, `.md`, or `.pdf`.

The in-memory adapter filters before scoring. Elasticsearch should place exact metadata filters inside the kNN `filter` clause.
