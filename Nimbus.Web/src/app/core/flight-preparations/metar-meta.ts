/**
 * The station and observation time of a METAR for a section header: "EBBR 100850Z 09004KT …" -> "EBBR · 08:50Z".
 * Read from the report text itself rather than remembered from the last fetch, so it is still right after a reload,
 * after the pilot edits the text, or when the METAR was pasted in. A METAR is recognised by its time group: free text
 * that merely starts with a four-letter word ("wind from the north") has none, so it gives null rather than a station.
 */
export function metarMeta(metar: string | null | undefined): string | null {
  const tokens = (metar ?? '').trim().split(/\s+/).filter(Boolean);
  let i = 0;
  if (/^(METAR|SPECI)$/i.test(tokens[i] ?? '')) i++;
  if (/^COR$/i.test(tokens[i] ?? '')) i++;

  const station = tokens[i];
  if (!station || !/^[A-Za-z0-9]{4}$/.test(station)) return null;

  for (const token of tokens.slice(i + 1, i + 4)) {
    const match = /^(\d{2})(\d{2})(\d{2})Z$/.exec(token);
    if (match && Number(match[1]) >= 1 && Number(match[1]) <= 31 && Number(match[2]) < 24 && Number(match[3]) < 60) {
      return `${station.toUpperCase()} · ${match[2]}:${match[3]}Z`;
    }
  }

  return null;
}
