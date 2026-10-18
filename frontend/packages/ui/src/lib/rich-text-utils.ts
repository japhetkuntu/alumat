/*
 * Plain functions for descriptions that may be plain text or the editor's small HTML. No browser needed, so servers and client code can both use them.
 */

// "Looks like markup": a tag-shaped thing. "2 < 3" or "<3" or "<jane@x.com>" stay plain text. Same rule as the server's.
const TAG = /<\s*\/?\s*[a-zA-Z][a-zA-Z0-9:-]*(\s[^<>]*)?\/?\s*>|<!--|<\?/;

export function looksLikeHtml(text?: string | null): boolean {
  return !!text && TAG.test(text);
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** Plain text as paragraphs: a blank line starts a new one, a single line break stays a line break. */
export function plainToRichHtml(text: string): string {
  return text.replace(/\r\n?/g, "\n").trim().split(/\n{2,}/).filter((p) => p.trim())
    .map((p) => `<p>${escapeHtml(p.trim()).replace(/\n/g, "<br>")}</p>`).join("");
}

/** What the editor should load: HTML as it is, old plain text turned into paragraphs. */
export function toEditorHtml(value?: string | null): string {
  if (!value || !value.trim()) return "";
  return looksLikeHtml(value) ? value : plainToRichHtml(value);
}

function decodeEntities(s: string): string {
  return s.replace(/&nbsp;/g, " ").replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&#39;|&apos;/g, "'").replace(/&amp;/g, "&");
}

/** The words alone, for cards, previews and share messages. Safe to call anywhere (no DOM needed). */
export function richTextToPlain(content?: string | null): string {
  if (!content) return "";
  if (!looksLikeHtml(content)) return content.trim();
  let s = content;
  s = s.replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1\s*>/gi, " ").replace(/<!--[\s\S]*?-->/g, " ");
  s = s.replace(/<br\s*\/?>/gi, "\n").replace(/<hr\s*\/?>/gi, "\n\n");
  s = s.replace(/<li\b[^>]*>/gi, "• ").replace(/<\/li\s*>/gi, "\n").replace(/<\/(ul|ol)\s*>/gi, "\n");
  s = s.replace(/<\/(p|h[1-6]|blockquote|div|pre|table)\s*>/gi, "\n\n");
  s = s.replace(/<[^>]*>/g, "");
  s = decodeEntities(s);
  return s.split("\n").map((l) => l.replace(/[ \t ]+/g, " ").trim()).join("\n").replace(/\n{3,}/g, "\n\n").trim();
}

/** A short plain-text taste of a description, cut at a word. */
export function richTextExcerpt(content?: string | null, max = 160): string {
  const text = richTextToPlain(content).replace(/\s+/g, " ").trim();
  if (text.length <= max) return text;
  const cut = text.slice(0, max);
  const space = cut.lastIndexOf(" ");
  return (space > max / 2 ? cut.slice(0, space) : cut).replace(/[ ,.;:-]+$/, "") + "…";
}

/** True when the editor holds nothing worth saving (an empty paragraph, only spaces). A divider alone counts as content. */
export function isRichTextEmpty(html?: string | null): boolean {
  if (!html) return true;
  if (/<hr\b/i.test(html)) return false;
  return richTextToPlain(html).length === 0;
}
