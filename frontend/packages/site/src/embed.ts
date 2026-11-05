const YT = /^[\w-]{6,20}$/;

/** The player address for a YouTube or Vimeo link, or null. It understands the same forms the server accepts (watch, short, embed and shorts links), so the editor shows the video before the page is saved. */
export function videoEmbed(url?: string | null): { src: string; watch: string } | null {
  if (!url) return null;
  try {
    const u = new URL(url.trim());
    if (u.protocol !== "https:") return null;
    const host = u.hostname.toLowerCase(), path = u.pathname.replace(/^\/|\/$/g, "");
    let yt: string | null = null;
    if (host === "youtu.be") yt = path;
    else if (host === "youtube.com" || host === "www.youtube.com" || host === "m.youtube.com") {
      if (path === "watch") yt = u.searchParams.get("v");
      else if (/^(embed|shorts|live)\//.test(path)) yt = path.split("/")[1];
    }
    if (yt && YT.test(yt)) return { src: `https://www.youtube-nocookie.com/embed/${yt}?rel=0`, watch: `https://www.youtube.com/watch?v=${yt}` };
    if ((host === "vimeo.com" || host === "www.vimeo.com") && /^\d{1,12}$/.test(path)) return { src: `https://player.vimeo.com/video/${path}?dnt=1`, watch: `https://vimeo.com/${path}` };
  } catch { /* not an address */ }
  return null;
}

/** The Facebook Page plugin address for a saved page link, or null. */
export function facebookEmbed(page?: string | null, height = 560): string | null {
  if (!page) return null;
  try {
    const u = new URL(page);
    if (u.protocol !== "https:" || !/^(www\.|m\.|web\.)?facebook\.com$/.test(u.hostname)) return null;
    const name = u.pathname.replace(/^\/|\/$/g, "").split("/")[0];
    if (!/^[\w.-]{3,80}$/.test(name)) return null;
    return `https://www.facebook.com/plugins/page.php?href=${encodeURIComponent(`https://www.facebook.com/${name}`)}&tabs=timeline&width=500&height=${height}&small_header=true&adapt_container_width=true&hide_cover=false&show_facepile=true`;
  } catch { return null; }
}

export const SOCIAL_NAMES: Record<string, string> = { facebook: "Facebook", instagram: "Instagram", linkedin: "LinkedIn", x: "X", youtube: "YouTube", tiktok: "TikTok" };
