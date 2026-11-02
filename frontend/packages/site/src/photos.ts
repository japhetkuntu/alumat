/**
 * Starting photos (Unsplash, free to use) so a new website looks finished before the institution adds its own. Each is just an address a
 * section's photo slot holds, so the admin replaces any of them by uploading, and nothing here is special-cased once replaced.
 */
const u = (id: string) => `https://images.unsplash.com/photo-${id}?auto=format&fit=crop&w=1100&q=70`;

export const STOCK = {
  hero: u("1594750852563-5ed8e0421d40"),
  graduate: u("1645263012710-35e2fc89cb5c"),
  toss: u("1627556704290-2b1f5853ff78"),
  campus: u("1670528148572-9270351b95bd"),
  library: u("1683319598210-d70486f2f996"),
  steps: [u("1686213011624-8578b598ef0f"), u("1622675363311-3e1904dc1885"), u("1517048676732-d65bc937f952")],
  cards: [u("1573164574572-cb89e39749b4"), u("1683319598210-d70486f2f996"), u("1731349219592-60ca16964631")],
  strip: [u("1594750852517-f37738fa2384"), u("1540575467063-178a50c2df87"), u("1683319598210-d70486f2f996")],
  gallery: [u("1594750852517-f37738fa2384"), u("1645263012710-35e2fc89cb5c"), u("1627556704290-2b1f5853ff78"), u("1587825140708-dfaf72ae4b04"), u("1670528148572-9270351b95bd"), u("1741707596397-efaae09503b5")],
};

/** Ask the image service for the width a slot needs. Only Unsplash addresses are resized this way; any other address is used as is. */
export function sized(url: string | undefined | null, width: number): string | undefined {
  if (!url) return undefined;
  try {
    const u = new URL(url);
    if (u.hostname !== "images.unsplash.com") return url;
    u.searchParams.set("w", String(width));
    u.searchParams.set("q", "70");
    u.searchParams.set("auto", "format");
    u.searchParams.set("fit", "crop");
    return u.toString();
  } catch { return url; }
}
