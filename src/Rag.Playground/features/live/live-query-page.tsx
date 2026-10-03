"use client";
import { Drawer } from "@/components/shared/drawer";
import { PageHeading, Panel } from "@/components/shared/panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { PipelineTrace } from "@/features/playground/pipeline-trace";
import { BookOpen, History, Play, Square, Terminal } from "lucide-react";
import { ClientDocuments } from "./client-documents";
import { LiveAnswerPanel } from "./live-answer-panel";
import { LiveEvidence } from "./live-evidence";
import { QueryControls } from "./query-controls";
import { LiveRetrievalChart } from "./retrieval-chart";
import {
    ClientError,
    ClientFooter,
    NoClientData,
    ProfileSelectors,
} from "./shared";
import { useLiveQuery } from "./use-live-query";

export function LiveQueryPage({ inspector = false }: { inspector?: boolean }) {
    const {
        question,
        setQuestion,
        setSettings,
        run,
        setRun,
        request,
        setRequest,
        trace,
        setTrace,
        running,
        setRunning,
        error,
        setError,
        notice,
        setNotice,
        history,
        historyOpen,
        setHistoryOpen,
        expanded,
        setExpanded,
        selected,
        setSelected,
        document,
        setDocument,
        copied,
        data,
        settings,
        abort,
        profile,
        evidence,
        execute,
        reveal,
        copy,
    } = useLiveQuery();
    return (
        <>
            <PageHeading
                eyebrow="CLIENT ENVIRONMENT"
                title={inspector ? "Retrieval inspector" : "Playground"}
                description={
                    inspector
                        ? "Inspect actual candidate scores, context admission, and ranking changes."
                        : "Query your documents through your configured models and retrieval services."
                }
                action={
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setHistoryOpen(true)}
                    >
                        <History size={14} />
                        Session history{" "}
                        {history.length > 0 && `(${history.length})`}
                    </Button>
                }
            />
            {data.error && (
                <ClientError message={data.error} retry={data.refresh} />
            )}
            {data.loading ? (
                <Skeleton className="h-60" />
            ) : !data.error && !data.corpora.length ? (
                <NoClientData />
            ) : (
                !data.error && (
                    <div className="playground-grid">
                        <div className="space-y-5">
                            <Panel
                                title="Query workspace"
                                icon={<Terminal size={14} />}
                                action={
                                    <Badge
                                        variant="outline"
                                        className="text-[9px]"
                                    >
                                        Connected API ·{" "}
                                        {data.capabilities?.embeddingModel ===
                                        "deterministic"
                                            ? "deterministic local models"
                                            : (data.capabilities?.chatModel ??
                                              data.capabilities
                                                  ?.embeddingModel)}
                                    </Badge>
                                }
                            >
                                <form
                                    onSubmit={(e) => {
                                        e.preventDefault();
                                        void execute();
                                    }}
                                >
                                    <ProfileSelectors
                                        {...data}
                                        onCorpus={data.setCorpusId}
                                        onProfile={data.setProfileId}
                                        disabled={running}
                                    />
                                    <div className="mt-3 flex justify-end">
                                        <Button
                                            type="button"
                                            variant="ghost"
                                            size="sm"
                                            disabled={!profile}
                                            onClick={() =>
                                                setDocument({
                                                    profileId: data.profileId,
                                                })
                                            }
                                        >
                                            <BookOpen size={12} />
                                            Browse corpus
                                        </Button>
                                    </div>
                                    <label className="mt-5 block">
                                        <span className="field-label">
                                            Ask a question
                                        </span>
                                        <textarea
                                            required
                                            maxLength={8000}
                                            rows={5}
                                            className="field !text-xs !leading-6"
                                            disabled={running}
                                            value={question}
                                            onChange={(e) =>
                                                setQuestion(e.target.value)
                                            }
                                            placeholder="Ask about the documents you indexed…"
                                            onKeyDown={(e) => {
                                                if (
                                                    (e.metaKey || e.ctrlKey) &&
                                                    e.key === "Enter" &&
                                                    !running &&
                                                    question.trim() &&
                                                    profile?.status ===
                                                        "ready" &&
                                                    profile.chunkCount
                                                ) {
                                                    e.preventDefault();
                                                    void execute();
                                                }
                                            }}
                                        />
                                    </label>
                                    <div className="mt-4 flex gap-2">
                                        {running ? (
                                            <Button
                                                type="button"
                                                className="flex-1"
                                                variant="outline"
                                                onClick={(e) => {
                                                    e.preventDefault();
                                                    abort.current?.abort();
                                                }}
                                            >
                                                <Square size={12} />
                                                Cancel query
                                            </Button>
                                        ) : (
                                            <Button
                                                type="submit"
                                                className="flex-1"
                                                disabled={
                                                    !question.trim() ||
                                                    profile?.status !==
                                                        "ready" ||
                                                    !profile?.chunkCount
                                                }
                                            >
                                                <Play size={12} />
                                                Run Query
                                            </Button>
                                        )}
                                        <Button
                                            type="button"
                                            variant="ghost"
                                            disabled={running}
                                            onClick={() => {
                                                setQuestion("");
                                                setRun(null);
                                                setTrace([]);
                                                setError("");
                                                setNotice("");
                                            }}
                                        >
                                            Clear
                                        </Button>
                                    </div>
                                    {profile && !profile.chunkCount && (
                                        <p className="mt-3 text-[10px] text-muted-foreground">
                                            Ingest documents into this profile
                                            before querying.
                                        </p>
                                    )}
                                </form>
                            </Panel>
                            <Panel title="Retrieval configuration">
                                <QueryControls
                                    settings={settings}
                                    onChange={setSettings}
                                    capabilities={data.capabilities}
                                    disabled={running}
                                />
                            </Panel>
                        </div>
                        <div className="min-w-0 space-y-5">
                            {error && (
                                <ClientError
                                    message={error}
                                    retry={() => void execute()}
                                />
                            )}
                            {notice && (
                                <p
                                    role="status"
                                    className="rounded border bg-card p-4 text-xs text-muted-foreground"
                                >
                                    {notice}
                                </p>
                            )}
                            <LiveAnswerPanel
                                run={run}
                                request={request}
                                running={running}
                                copied={copied}
                                reveal={reveal}
                                copy={copy}
                            />
                            {run && (
                                <>
                                    <Panel>
                                        <LiveRetrievalChart
                                            chunks={run.candidates}
                                            threshold={
                                                request?.minRelevance ?? 0
                                            }
                                            selected={selected}
                                            onSelect={reveal}
                                        />
                                    </Panel>
                                    {inspector && selected && (
                                        <Panel title="Query / chunk comparison">
                                            <div className="grid gap-4 sm:grid-cols-2">
                                                <div>
                                                    <h3 className="eyebrow mb-2">
                                                        Query
                                                    </h3>
                                                    <p className="text-xs leading-6">
                                                        {run.question}
                                                    </p>
                                                </div>
                                                <div>
                                                    <h3 className="eyebrow mb-2">
                                                        Selected evidence
                                                    </h3>
                                                    <HighlightedText
                                                        query={run.question}
                                                        text={
                                                            evidence.find(
                                                                (c) =>
                                                                    c.id ===
                                                                    selected,
                                                            )?.content ?? ""
                                                        }
                                                    />
                                                    <p className="mt-3 text-[10px] text-muted-foreground">
                                                        Highlights show lexical
                                                        overlap, not a semantic
                                                        attribution score.
                                                    </p>
                                                </div>
                                            </div>
                                        </Panel>
                                    )}
                                    <section>
                                        <h2 className="panel-title mb-3">
                                            <BookOpen size={15} />
                                            Retrieved Context
                                            <span className="mono ml-auto text-[10px] text-muted-foreground">
                                                {run.context.length} admitted /{" "}
                                                {run.candidates.length}{" "}
                                                retrieved
                                            </span>
                                        </h2>
                                        <LiveEvidence
                                            chunks={evidence}
                                            expanded={expanded}
                                            selected={selected}
                                            onToggle={(id) => {
                                                setSelected(id);
                                                setExpanded((current) =>
                                                    current.includes(id)
                                                        ? current.filter(
                                                              (item) =>
                                                                  item !== id,
                                                          )
                                                        : [...current, id],
                                                );
                                            }}
                                            onDocument={(id) =>
                                                setDocument({
                                                    profileId: run.profileId,
                                                    id,
                                                })
                                            }
                                        />
                                    </section>
                                </>
                            )}
                            <PipelineTrace
                                stages={trace}
                                running={running}
                                simulated={false}
                                totalLatencyMs={run?.totalLatencyMs}
                            />
                        </div>
                    </div>
                )
            )}
            {document && (
                <ClientDocuments
                    key={`${document.profileId}-${document.id}`}
                    profileId={document.profileId}
                    documentId={document.id}
                    onClose={() => setDocument(null)}
                />
            )}
            <Drawer
                open={historyOpen}
                onOpenChange={setHistoryOpen}
                title="Client query history"
                description="Up to 20 runs held in memory on this page. Export any result to save it explicitly."
            >
                <div className="space-y-3 py-5">
                    {!history.length && (
                        <p className="text-xs text-muted-foreground">
                            No client queries in this session.
                        </p>
                    )}
                    {history.map((item) => (
                        <button
                            className="block w-full rounded-lg border p-4 text-left hover:bg-accent/30"
                            key={item.run.id}
                            onClick={() => {
                                abort.current?.abort();
                                abort.current = null;
                                setRunning(false);
                                setRun(item.run);
                                setRequest(item.request);
                                setTrace(item.run.trace);
                                setQuestion(item.request.question);
                                setSettings(item.request);
                                data.setCorpusId(item.request.corpusId);
                                data.setProfileId(item.request.profileId);
                                setHistoryOpen(false);
                                setError("");
                            }}
                        >
                            <p className="text-xs leading-6">
                                {item.run.question}
                            </p>
                            <p className="mono mt-2 text-[10px] text-muted-foreground">
                                {item.request.mode} · Top {item.request.topK} ·{" "}
                                {item.run.totalLatencyMs.toFixed(1)} ms
                            </p>
                        </button>
                    ))}
                </div>
            </Drawer>
            <ClientFooter />
        </>
    );
}
function HighlightedText({ text, query }: { text: string; query: string }) {
    const words = new Set(
        query.toLowerCase().match(/[\p{L}\p{N}]{3,}/gu) ?? [],
    );
    return (
        <p className="text-xs leading-6">
            {text.split(/([\p{L}\p{N}]+)/gu).map((part, index) =>
                words.has(part.toLowerCase()) ? (
                    <mark
                        key={index}
                        className="rounded bg-accent px-0.5 text-primary"
                    >
                        {part}
                    </mark>
                ) : (
                    part
                ),
            )}
        </p>
    );
}
