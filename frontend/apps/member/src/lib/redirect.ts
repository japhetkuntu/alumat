/**
 * Guards a post-auth `?redirect=` destination against open-redirect abuse.
 * `path.startsWith("/")` alone isn't enough: "//evil.com" also starts with "/" but browsers treat a
 * leading "//" as protocol-relative (an absolute cross-origin URL), and a leading backslash is
 * normalised to "/" before parsing ("/\evil.com" -> "//evil.com"). Only a genuine same-origin path is safe.
 */
export function isSafeRedirectPath(path: string | null | undefined): path is string {
  return !!path && path.startsWith("/") && path[1] !== "/" && path[1] !== "\\";
}

/** Appends `redirect` (when safe) to an auth-page link so the intended destination survives login <-> register. */
export function withRedirect(href: string, redirect: string | null | undefined): string {
  if (!isSafeRedirectPath(redirect)) return href;
  return `${href}${href.includes("?") ? "&" : "?"}redirect=${encodeURIComponent(redirect)}`;
}
