"use client";
import { ChartFrame } from "@/components/charts/chart-frame";
import { Panel } from "@/components/shared/panel";
import type { EvaluationRun } from "@/lib/live/contracts";
import {
    Bar,
    BarChart,
    CartesianGrid,
    ResponsiveContainer,
    Scatter,
    ScatterChart,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";
import { percentage } from "./evaluation-metrics";
type Resource = "embeddingOperations" | "averageContextTokens";
export function EvaluationCharts({
    chartData,
    resource,
    setResource,
}: {
    chartData: (EvaluationRun["profiles"][number] & {
        recall: number | null;
    })[];
    resource: Resource;
    setResource: (resource: Resource) => void;
}) {
    return (
        <div className="my-5 grid gap-5 xl:grid-cols-2">
            <Panel>
                <ChartFrame
                    title="Recall by indexing profile"
                    columns={["Profile", "Recall@5"]}
                    rows={chartData.map((p) => [
                        p.profileName,
                        percentage(p.metrics.recallAt5),
                    ])}
                >
                    <ResponsiveContainer width="100%" height="100%">
                        <BarChart
                            data={chartData}
                            margin={{ left: -20, right: 10 }}
                        >
                            <CartesianGrid
                                vertical={false}
                                stroke="var(--border)"
                            />
                            <XAxis
                                dataKey="profileName"
                                tick={{ fontSize: 10 }}
                            />
                            <YAxis domain={[0, 100]} tick={{ fontSize: 10 }} />
                            <Tooltip
                                contentStyle={{
                                    background: "var(--card)",
                                    fontSize: 11,
                                }}
                            />
                            <Bar
                                dataKey="recall"
                                name="Recall@5 (%)"
                                fill="#6863d9"
                                maxBarSize={50}
                                isAnimationActive={false}
                            />
                        </BarChart>
                    </ResponsiveContainer>
                </ChartFrame>
            </Panel>
            <Panel>
                <label className="mb-3 block">
                    <span className="sr-only">Quality resource axis</span>
                    <select
                        className="field !w-auto"
                        value={resource}
                        onChange={(e) =>
                            setResource(
                                e.target.value === "averageContextTokens"
                                    ? "averageContextTokens"
                                    : "embeddingOperations",
                            )
                        }
                    >
                        <option value="embeddingOperations">
                            Recall vs embedding operations
                        </option>
                        <option value="averageContextTokens">
                            Recall vs context tokens
                        </option>
                    </select>
                </label>
                <ChartFrame
                    title="Quality vs resources"
                    description="Measured operations and estimated context tokens. No pricing assumptions."
                    columns={["Profile", resource, "Recall@5"]}
                    rows={chartData.map((p) => [
                        p.profileName,
                        p[resource],
                        percentage(p.metrics.recallAt5),
                    ])}
                    height={190}
                >
                    <ResponsiveContainer width="100%" height="100%">
                        <ScatterChart margin={{ left: -20, right: 15 }}>
                            <CartesianGrid stroke="var(--border)" />
                            <XAxis
                                type="number"
                                dataKey={resource}
                                name={resource}
                                padding={{ left: 8, right: 8 }}
                                tick={{ fontSize: 10 }}
                            />
                            <YAxis
                                type="number"
                                dataKey="recall"
                                name="Recall@5 (%)"
                                domain={[0, 100]}
                                padding={{ top: 8, bottom: 8 }}
                                tick={{ fontSize: 10 }}
                            />
                            <Tooltip
                                contentStyle={{
                                    background: "var(--card)",
                                    fontSize: 11,
                                }}
                            />
                            <Scatter
                                data={chartData.filter(
                                    (p) => p.recall !== null,
                                )}
                                fill="#6863d9"
                                isAnimationActive={false}
                            />
                        </ScatterChart>
                    </ResponsiveContainer>
                </ChartFrame>
            </Panel>
        </div>
    );
}
