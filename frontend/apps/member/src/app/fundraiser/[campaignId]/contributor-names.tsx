// Many names read best set as an even alphabetical list; a handful read best as a single flowing line.
const INDEX_THRESHOLD = 24;
const MAX_SHOWN = 600;

function surname(name: string) {
  const parts = name.trim().split(/\s+/);
  return parts[parts.length - 1] ?? name;
}

export function ContributorNames({ names }: { names: string[] }) {
  const shown = names.slice(0, MAX_SHOWN);
  const more = names.length - shown.length;

  if (names.length <= INDEX_THRESHOLD) {
    return (
      <ul className="mx-auto mt-7 flex max-w-xl flex-wrap justify-center gap-x-3 gap-y-2 text-center text-[17px] leading-snug">
        {shown.map((n, i) => (
          <li key={`${n}-${i}`} className="flex items-center gap-3">
            {i > 0 && <span aria-hidden className="text-border">·</span>}
            <span>{n}</span>
          </li>
        ))}
      </ul>
    );
  }

  // Alphabetical by surname, flowing evenly down the columns.
  const sorted = [...shown].sort((a, b) => surname(a).localeCompare(surname(b)) || a.localeCompare(b));

  return (
    <div className="mt-8">
      <ul className="columns-2 gap-x-8 sm:columns-3 text-[14.5px] leading-snug">
        {sorted.map((n, i) => (
          <li key={`${n}-${i}`} className="break-inside-avoid border-b border-border/40 py-2">{n}</li>
        ))}
      </ul>
      {more > 0 && <p className="mt-4 text-center text-[13px] text-muted-foreground">and {more.toLocaleString("en-GH")} more</p>}
    </div>
  );
}
