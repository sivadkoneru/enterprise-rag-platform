# Demo #1 — cold clone to a grounded, cited answer.
# Record with:  vhs docs/demo/quickstart.tape
#
# Why the API and not the CLI: both stores default to `memory`, and the CLI
# process is short-lived, so `ingest` and `query` in two CLI invocations do not
# share state. The API is one long-running process, so it retrieves what it just
# ingested with no Docker, no .env, and no API key.

Output docs/demo/quickstart.gif

Require dotnet
Require curl
Require jq

Set Shell "bash"
Set FontSize 18
Set Width 1200
Set Height 760
Set Padding 20
Set TypingSpeed 35ms
Set Theme "Catppuccin Mocha"

# ---------------------------------------------------------------- hidden setup
Hide
Type "cd $(git rev-parse --show-toplevel)"
Enter
Type "export ASPNETCORE_URLS=http://localhost:5080"
Enter
Type "export LOCAL_SOURCE_ALLOWED_ROOTS=$PWD"
Enter
# Absolute path: `dotnet run --project` uses the project dir as the content root,
# so a relative ./samples would not resolve from the repo root.
Type `jq -nc --arg p "$PWD/samples" '{path:$p}' > ingest.json`
Enter
# Warm the build so the visible `dotnet build` below is a real but incremental
# invocation instead of a five-minute restore.
Type "dotnet build --nologo -v q > /dev/null 2>&1"
Enter
Type "clear"
Enter
Show

# ---------------------------------------------------------------------- premise
Type "# No .env, no Docker, no API key. Defaults: in-memory stores, deterministic LLM."
Enter
Sleep 2s

Type "dotnet build --nologo -v q"
Enter
Sleep 6s

# ------------------------------------------------------------------ start the API
Type "dotnet run --project src/Rag.Api --no-build > api.log 2>&1 &"
Enter
Sleep 8s

Type "curl -s localhost:5080/health | jq"
Enter
Sleep 3s

# ---------------------------------------------------------------------- ingest
Type "cat ingest.json"
Enter
Sleep 2s

Type `JOB=$(curl -s -X POST localhost:5080/documents -H 'content-type: application/json' -d @ingest.json | jq -r .jobId); echo "jobId=$JOB"`
Enter
Sleep 3s

Type `curl -s "localhost:5080/jobs/$JOB" | jq`
Enter
Sleep 4s

# ----------------------------------------------------------- the actual payoff
Type "# Same process, so the in-memory index is still warm."
Enter
Sleep 1500ms

Type `curl -s -X POST localhost:5080/query -H 'content-type: application/json' -d '{"question":"What is the refund policy?","topK":5}' | jq '{answer, citations: [.citations[] | {source, chunkIndex, score}]}'`
Enter
Sleep 8s

# --------------------------------------------------------------------- teardown
Hide
Type "kill %1; rm -f ingest.json api.log"
Enter
Show
