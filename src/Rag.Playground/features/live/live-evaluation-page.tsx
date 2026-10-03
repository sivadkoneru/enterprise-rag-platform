"use client";
import { EvaluationCharts } from "./evaluation-charts";
import { Drawer } from "@/components/shared/drawer";
import { Hint } from "@/components/shared/hint";
import { PageHeading, Panel } from "@/components/shared/panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { evaluationTemplate } from "@/lib/live/evaluation";
import { downloadJson } from "@/lib/live/http";
import { Download, FileJson, FlaskConical, Search, Upload } from "lucide-react";
import { ClientDocuments } from "./client-documents";
import { JobProgress } from "./job-progress";
import { LiveEvidence } from "./live-evidence";
import { QueryControls } from "./query-controls";
import { RecentJobs } from "./recent-jobs";
import { ClientError, ClientFooter, NoClientData } from "./shared";
import { useLiveEvaluation } from "./use-live-evaluation";

import { metricDefinitions, percentage } from "./evaluation-metrics";

export function LiveEvaluationPage() {
    const {
        setSettings,
        difficulty,
        setDifficulty,
        dataset,
        setDataset,
        setSelectedProfiles,
        job,
        setJob,
        report,
        setReport,
        reports,
        setActiveProfile,
        error,
        busy,
        search,
        setSearch,
        resultFilter,
        setResultFilter,
        selected,
        setSelected,
        expanded,
        setExpanded,
        document,
        setDocument,
        resource,
        setResource,
        page,
        setPage,
        data,
        settings,
        resultProfile,
        eligibleIds,
        cases,
        chartData,
        run,
        completed,
        importFile,
        status,
    } = useLiveEvaluation();
    return (
        <>
            <PageHeading
                eyebrow="CLIENT EVALUATION"
                title="Evaluate your RAG workflow"
                description="Measure retrieval and citation behavior against evidence labels from your own corpus."
                action={
                    <Button
                        size="sm"
                        variant="outline"
                        onClick={() =>
                            downloadJson(
                                "evaluation-template.json",
                                evaluationTemplate,
                            )
                        }
                    >
                        <Download size={13} />
                        Dataset template
                    </Button>
                }
            />
            {(error || data.error) && (
                <ClientError
                    message={error || data.error}
                    retry={data.refresh}
                />
            )}
            {!data.loading && !data.error && !data.corpora.length ? (
                <NoClientData />
            ) : (
                <div className="mb-6 grid gap-5 xl:grid-cols-[320px_minmax(0,1fr)]">
                    <Panel
                        title="Experiment profiles"
                        icon={<FlaskConical size={14} />}
                    >
                        <label className="block">
                            <span className="field-label">Corpus</span>
                            <select
                                className="field"
                                value={data.corpusId}
                                onChange={(e) =>
                                    data.setCorpusId(e.target.value)
                                }
                            >
                                <option value="" disabled>
                                    Select a corpus
                                </option>
                                {data.corpora.map((c) => (
                                    <option value={c.id} key={c.id}>
                                        {c.name}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <p className="my-4 text-[11px] leading-6 text-muted-foreground">
                            Select indexed profiles from the same corpus. Each
                            run uses the same questions and Top K 5 for
                            comparable retrieval metrics.
                        </p>
                        <div className="space-y-3">
                            {data.profiles.map((p) => (
                                <label
                                    key={p.id}
                                    className="flex items-start gap-3 rounded-md border p-3 text-xs"
                                >
                                    <input
                                        className="mt-0.5 accent-primary"
                                        type="checkbox"
                                        disabled={
                                            p.status !== "ready" ||
                                            !p.chunkCount
                                        }
                                        checked={eligibleIds.includes(p.id)}
                                        onChange={(e) =>
                                            setSelectedProfiles((current) =>
                                                e.target.checked
                                                    ? [...current, p.id]
                                                    : current.filter(
                                                          (id) => id !== p.id,
                                                      ),
                                            )
                                        }
                                    />
                                    <span>
                                        <strong className="font-medium">
                                            {p.name}
                                        </strong>
                                        <span className="mt-1 block text-[10px] text-muted-foreground">
                                            {p.strategy} · {p.chunkCount} chunks
                                        </span>
                                    </span>
                                </label>
                            ))}
                        </div>
                        <details className="mt-4 border-t pt-4">
                            <summary className="mb-4 text-xs font-medium">
                                Evaluation retrieval settings
                            </summary>
                            <QueryControls
                                fixedTopK
                                settings={settings}
                                onChange={(next) =>
                                    setSettings({ ...next, topK: 5 })
                                }
                                capabilities={data.capabilities}
                                disabled={busy}
                            />
                            <p className="mt-2 text-[10px] text-muted-foreground">
                                Top K is fixed at 5 for these evaluation
                                metrics.
                            </p>
                        </details>
                    </Panel>
                    <Panel
                        title="Evaluation questions"
                        icon={<FileJson size={14} />}
                        action={
                            <label className="cursor-pointer text-xs text-primary">
                                <span className="flex items-center gap-1">
                                    <Upload size={12} />
                                    Import JSON
                                </span>
                                <input
                                    type="file"
                                    accept=".json,application/json"
                                    className="sr-only"
                                    onChange={(e) =>
                                        void importFile(e.target.files?.[0])
                                    }
                                />
                            </label>
                        }
                    >
                        <label className="block">
                            <span className="sr-only">
                                Evaluation dataset JSON
                            </span>
                            <textarea
                                className="field mono min-h-48 !text-[11px] !leading-5"
                                value={dataset}
                                onChange={(e) => setDataset(e.target.value)}
                                placeholder={
                                    '{"questions": [{"id": "case-1", "question": "…", "expectedSourceFile": "policy.md", "goldAnchors": [{"phrase": "verbatim evidence"}]}]}'
                                }
                            />
                        </label>
                        <p className="mt-3 text-[11px] leading-6 text-muted-foreground">
                            Use exact source filenames and verbatim evidence
                            phrases. Missing or ambiguous anchors are rejected
                            before execution. Model calls use your configured
                            endpoint.
                        </p>
                        <Button
                            className="mt-4"
                            size="sm"
                            disabled={
                                busy ||
                                !eligibleIds.length ||
                                !dataset.trim() ||
                                (!!job &&
                                    ["queued", "running", "paused"].includes(
                                        job.status,
                                    ))
                            }
                            onClick={() => void run()}
                        >
                            <FlaskConical size={12} />
                            Run evaluation
                        </Button>
                        {job && (
                            <div className="mt-4">
                                <JobProgress
                                    key={job.id}
                                    initial={job}
                                    onComplete={(next) => {
                                        setJob(next);
                                        void completed(next);
                                    }}
                                />
                            </div>
                        )}
                        <RecentJobs
                            kind="evaluation"
                            onSelect={(next) => {
                                setJob(next);
                                void completed(next);
                            }}
                        />
                    </Panel>
                </div>
            )}
            {report && resultProfile && (
                <>
                    <div className="mb-4 flex flex-wrap items-center gap-3">
                        <Badge variant="outline">
                            Client results · {report.status}
                        </Badge>
                        <select
                            aria-label="Evaluation run"
                            className="field !w-auto"
                            value={report.id}
                            onChange={(e) => {
                                const item = reports.find(
                                    (r) => r.id === e.target.value,
                                );
                                if (item) {
                                    setReport(item);
                                    setActiveProfile(
                                        item.profiles[0]?.profileId ?? "",
                                    );
                                    setPage(0);
                                }
                            }}
                        >
                            {reports.map((r) => (
                                <option key={r.id} value={r.id}>
                                    Run {r.id.slice(0, 8)}
                                </option>
                            ))}
                        </select>
                        <select
                            aria-label="Result profile"
                            className="field !w-auto"
                            value={resultProfile.profileId}
                            onChange={(e) => {
                                setActiveProfile(e.target.value);
                                setPage(0);
                            }}
                        >
                            {report.profiles.map((p) => (
                                <option key={p.profileId} value={p.profileId}>
                                    {p.profileName}
                                </option>
                            ))}
                        </select>
                        <Button
                            size="sm"
                            variant="outline"
                            className="ml-auto"
                            onClick={() =>
                                downloadJson(
                                    `evaluation-${report.id}.json`,
                                    report,
                                )
                            }
                        >
                            <Download size={12} />
                            Export results
                        </Button>
                    </div>
                    <div className="mb-5 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-7">
                        {metricDefinitions.map((metric) => (
                            <Panel key={metric.key} bodyClassName="!p-4">
                                <div className="flex items-center justify-between gap-1 text-[10px] text-muted-foreground">
                                    {metric.label}
                                    <Hint text={metric.definition} />
                                </div>
                                <p className="mono mt-3 text-xl font-semibold">
                                    {percentage(
                                        resultProfile.metrics[metric.key],
                                    )}
                                </p>
                            </Panel>
                        ))}
                    </div>
                    <p className="mb-5 text-[11px] leading-6 text-muted-foreground">
                        {resultProfile.outcomes.length} labeled cases ·
                        Unavailable metrics display —. Lexical overlap does not
                        establish semantic support. These results are
                        independent of the published deterministic benchmark.
                    </p>
                    <Panel
                        title="Profile comparison"
                        bodyClassName="overflow-x-auto"
                    >
                        <table className="data-table w-full">
                            <thead>
                                <tr>
                                    {[
                                        "Profile",
                                        "Recall@5",
                                        "Embedding operations",
                                        "Average context tokens",
                                        "Average latency",
                                    ].map((label) => (
                                        <th key={label}>{label}</th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {report.profiles.map((p) => (
                                    <tr key={p.profileId}>
                                        <th>{p.profileName}</th>
                                        <td>
                                            {percentage(p.metrics.recallAt5)}
                                        </td>
                                        <td>{p.embeddingOperations}</td>
                                        <td>
                                            {p.averageContextTokens.toFixed(1)}
                                        </td>
                                        <td>
                                            {p.averageLatencyMs.toFixed(1)} ms
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </Panel>
                    <EvaluationCharts
                        chartData={chartData}
                        resource={resource}
                        setResource={setResource}
                    />
                    <Panel title="Evaluation cases">
                        <div className="mb-4 flex flex-wrap gap-3">
                            <label className="relative min-w-52 flex-1">
                                <Search
                                    size={13}
                                    className="absolute left-3 top-3 text-muted-foreground"
                                />
                                <input
                                    className="field !pl-9"
                                    aria-label="Search client evaluation cases"
                                    placeholder="Search questions…"
                                    value={search}
                                    onChange={(e) => {
                                        setSearch(e.target.value);
                                        setPage(0);
                                    }}
                                />
                            </label>
                            <select
                                aria-label="Difficulty filter"
                                className="field !w-auto"
                                value={difficulty}
                                onChange={(e) => {
                                    setDifficulty(e.target.value);
                                    setPage(0);
                                }}
                            >
                                <option value="all">All difficulties</option>
                                {[
                                    ...new Set(
                                        (report.questions ?? []).map(
                                            (q) => q.difficulty ?? "custom",
                                        ),
                                    ),
                                ].map((value) => (
                                    <option key={value} value={value}>
                                        {value}
                                    </option>
                                ))}
                            </select>
                            <select
                                aria-label="Evaluation result filter"
                                className="field !w-auto"
                                value={resultFilter}
                                onChange={(e) => {
                                    setResultFilter(e.target.value);
                                    setPage(0);
                                }}
                            >
                                {[
                                    "all",
                                    "Pass",
                                    "Retrieval miss",
                                    "Citation mismatch",
                                    "Abstained correctly",
                                    "Unexpected answer",
                                ].map((value) => (
                                    <option key={value} value={value}>
                                        {value === "all"
                                            ? "All results"
                                            : value}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div className="overflow-x-auto">
                            <table className="data-table w-full">
                                <thead>
                                    <tr>
                                        <th>Question</th>
                                        <th>Difficulty</th>
                                        <th>Expected source</th>
                                        <th>Retrieved source</th>
                                        <th>Recall@5</th>
                                        <th>Citation correct</th>
                                        <th>Result</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {cases
                                        .slice(page * 10, page * 10 + 10)
                                        .map((item) => (
                                            <tr key={item.questionId}>
                                                <td>
                                                    <button
                                                        className="max-w-lg text-left text-primary hover:underline"
                                                        onClick={() => {
                                                            setSelected(item);
                                                            setExpanded([]);
                                                        }}
                                                    >
                                                        {item.question}
                                                    </button>
                                                </td>
                                                <td>
                                                    {report.questions?.find(
                                                        (q) =>
                                                            q.id ===
                                                            item.questionId,
                                                    )?.difficulty ?? "custom"}
                                                </td>
                                                <td>
                                                    {item.expectedSourceFile ??
                                                        "No evidence expected"}
                                                </td>
                                                <td>
                                                    {[
                                                        ...new Set(
                                                            item.run.candidates.map(
                                                                (c) =>
                                                                    c.filename,
                                                            ),
                                                        ),
                                                    ].join(", ") || "None"}
                                                </td>
                                                <td>
                                                    {percentage(
                                                        item.metrics.recallAt5,
                                                    )}
                                                </td>
                                                <td>
                                                    {item.metrics
                                                        .citationAccuracy ==
                                                    null
                                                        ? "—"
                                                        : item.metrics
                                                                .citationAccuracy
                                                          ? "Yes"
                                                          : "No"}
                                                </td>
                                                <td>
                                                    <Badge variant="outline">
                                                        {status(item)}
                                                    </Badge>
                                                </td>
                                            </tr>
                                        ))}
                                </tbody>
                            </table>
                        </div>
                        <div className="mt-4 flex items-center justify-between text-xs">
                            <Button
                                size="sm"
                                variant="outline"
                                disabled={!page}
                                onClick={() => setPage(page - 1)}
                            >
                                Previous
                            </Button>
                            <span>
                                {cases.length} cases · Page {page + 1}
                            </span>
                            <Button
                                size="sm"
                                variant="outline"
                                disabled={(page + 1) * 10 >= cases.length}
                                onClick={() => setPage(page + 1)}
                            >
                                Next
                            </Button>
                        </div>
                    </Panel>
                </>
            )}
            <Drawer
                open={!!selected}
                onOpenChange={(open) => {
                    if (!open) setSelected(null);
                }}
                title="Evaluation case"
                description="Expected labels and actual generated output from the selected client run."
            >
                {selected && (
                    <div className="space-y-5 py-5">
                        <h3 className="text-sm font-semibold leading-6">
                            {selected.question}
                        </h3>
                        <div className="rounded-lg border p-4">
                            <h4 className="eyebrow mb-2">Expected answer</h4>
                            <p className="text-xs leading-6">
                                {selected.expectedAnswer ??
                                    (selected.expectedAbstention
                                        ? "Abstain: no supporting evidence expected."
                                        : "No reference answer supplied; scoring uses evidence anchors.")}
                            </p>
                            <p className="mt-2 text-[10px] text-muted-foreground">
                                Expected source:{" "}
                                {selected.expectedSourceFile ?? "None"}
                            </p>
                            <p className="mt-2 text-[10px] leading-5 text-muted-foreground">
                                Expected evidence:{" "}
                                {report?.questions
                                    ?.find((q) => q.id === selected.questionId)
                                    ?.goldAnchors.map((a) => a.phrase)
                                    .join(" · ") || "No evidence anchors"}
                            </p>
                        </div>
                        <div className="rounded-lg border p-4">
                            <h4 className="eyebrow mb-2">Generated answer</h4>
                            <p className="whitespace-pre-wrap text-xs leading-6">
                                {selected.run.answer}
                            </p>
                            <p className="mono mt-3 break-all text-[10px]">
                                Generated citations:{" "}
                                {selected.run.citations
                                    .map(
                                        (c) =>
                                            `${c.chunkId} (${c.valid ? "valid" : "invalid"})`,
                                    )
                                    .join(", ") || "None"}
                            </p>
                        </div>
                        <dl className="grid grid-cols-2 gap-3">
                            {metricDefinitions.map((m) => (
                                <div
                                    key={m.key}
                                    className="rounded border p-3 text-xs"
                                >
                                    <dt className="text-muted-foreground">
                                        {m.label}
                                    </dt>
                                    <dd className="mono mt-2">
                                        {percentage(selected.metrics[m.key])}
                                    </dd>
                                </div>
                            ))}
                        </dl>
                        <h4 className="eyebrow">Retrieved evidence</h4>
                        <LiveEvidence
                            chunks={selected.run.candidates}
                            selected={null}
                            expanded={expanded}
                            onToggle={(id) =>
                                setExpanded((current) =>
                                    current.includes(id)
                                        ? current.filter((c) => c !== id)
                                        : [...current, id],
                                )
                            }
                            onDocument={(id) =>
                                setDocument({
                                    profile: selected.run.profileId,
                                    id,
                                })
                            }
                        />
                    </div>
                )}
            </Drawer>
            {document && (
                <ClientDocuments
                    profileId={document.profile}
                    documentId={document.id}
                    onClose={() => setDocument(null)}
                />
            )}
            <ClientFooter />
        </>
    );
}
