"use client";
import { Bar, BarChart, CartesianGrid, Cell, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartFrame } from "@/components/charts/chart-frame";
import type { LiveChunk } from "@/lib/live/contracts";
import { score } from "./live-evidence";

export function LiveRetrievalChart({ chunks, threshold, selected, onSelect }: { chunks: LiveChunk[]; threshold: number; selected: string | null; onSelect: (id: string) => void }) {
    const data = chunks.filter(c => !c.isNeighbor).map(c => ({ ...c, label: `#${c.rankAfter} · ${c.index}` }));
    return <ChartFrame title="Retrieval score distribution" description="Normalized cosine similarity. The dashed line is the context admission threshold." columns={["Chunk", "Source", "Normalized cosine", "RRF", "Reranker", "Context"]} rows={data.map(c => [c.id, c.filename, score(c.vectorScore), score(c.fusionScore), score(c.rerankerScore), c.inContext ? "Admitted" : "Excluded"])}><ResponsiveContainer width="100%" height="100%"><BarChart data={data} margin={{ left: -20, right: 10, top: 12 }}><CartesianGrid vertical={false} stroke="var(--border)" /><XAxis dataKey="label" tick={{ fontSize: 10 }} axisLine={false} tickLine={false} /><YAxis domain={[0, 1]} tick={{ fontSize: 10 }} axisLine={false} tickLine={false} /><Tooltip contentStyle={{ background: "var(--card)", border: "1px solid var(--border)", borderRadius: 6, fontSize: 11 }} /><ReferenceLine y={threshold} stroke="var(--muted-foreground)" strokeDasharray="4 4" /><Bar dataKey="vectorScore" name="Normalized cosine" radius={[3, 3, 0, 0]} maxBarSize={48} isAnimationActive={false}>{data.map(c => <Cell key={c.id} fill={c.id === selected ? "#7c72ed" : c.inContext ? "#6863d9" : "#a0a4b7"} cursor="pointer" onClick={() => onSelect(c.id)} />)}</Bar></BarChart></ResponsiveContainer></ChartFrame>;
}
