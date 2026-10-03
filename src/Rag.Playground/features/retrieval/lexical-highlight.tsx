"use client";
export function Highlight({ text, concepts }: { text: string; concepts: string[] }) {
    const terms = [
        ...new Set(
            concepts
                .flatMap((concept) => concept.split(/\s+/))
                .filter((term) => term.length > 2),
        ),
    ];
    if (!terms.length) return <>{text}</>;
    const expression = new RegExp(
        `(${terms.map((term) => term.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")})`,
        "gi",
    );
    const lookup = new Set(terms.map((term) => term.toLowerCase()));
    return (
        <>
            {text.split(expression).map((part, index) =>
                lookup.has(part.toLowerCase()) ? (
                    <mark
                        key={index}
                        className="rounded-sm bg-accent px-0.5 text-accent-foreground"
                    >
                        {part}
                    </mark>
                ) : (
                    part
                ),
            )}
        </>
    );
}
