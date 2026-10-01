"use client";
import { useState, type ReactNode } from "react";
import { Table2, ChartNoAxesCombined } from "lucide-react";
import { Button } from "@/components/ui/button";

/** A responsive chart surface with an equivalent keyboard-accessible data table. */
export function ChartFrame({
    title,
    description,
    columns,
    rows,
    children,
    height = 230,
}: {
    title: string;
    description?: string;
    columns: string[];
    rows: (string | number)[][];
    children: ReactNode;
    height?: number;
}) {
    const [table, setTable] = useState(false);
    return (
        <div className="min-w-0">
            <div className="mb-3 flex items-start justify-between gap-3">
                <div>
                    <h3 className="text-xs font-semibold">{title}</h3>
                    {description && (
                        <p className="text-muted mt-1 text-[11px] leading-4">
                            {description}
                        </p>
                    )}
                </div>
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setTable(!table)}
                    aria-label={`${table ? "Show chart" : "Show data table"}: ${title}`}
                    aria-pressed={table}
                >
                    {table ? (
                        <ChartNoAxesCombined size={14} />
                    ) : (
                        <Table2 size={14} />
                    )}
                </Button>
            </div>
            {table ? (
                <div className="overflow-auto" style={{ minHeight: height }}>
                    <table className="data-table w-full text-xs">
                        <caption className="sr-only">{title}</caption>
                        <thead>
                            <tr>
                                {columns.map((column) => (
                                    <th key={column} scope="col">
                                        {column}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map((row, i) => (
                                <tr key={i}>
                                    {row.map((cell, j) =>
                                        j === 0 ? (
                                            <th key={j} scope="row">
                                                {cell}
                                            </th>
                                        ) : (
                                            <td key={j}>{cell}</td>
                                        ),
                                    )}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            ) : (
                <div
                    role="img"
                    aria-label={`${title}. Use Show data table for exact values.`}
                    className="min-w-0"
                    style={{ height }}
                >
                    {children}
                </div>
            )}
        </div>
    );
}
