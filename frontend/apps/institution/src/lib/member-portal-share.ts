export function buildMemberPortalShareUrl(path: string, memberPortalUrl?: string | null) {
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;

  if (memberPortalUrl && memberPortalUrl.trim()) {
    const cleanBase = memberPortalUrl.trim().replace(/\/$/, "");
    return new URL(normalizedPath, `${cleanBase}/`).toString();
  }

  if (typeof window !== "undefined") {
    return new URL(normalizedPath, window.location.origin).toString();
  }

  return normalizedPath;
}
