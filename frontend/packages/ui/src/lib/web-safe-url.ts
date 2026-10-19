/**
 * Photos uploaded before file names were cleaned up are stored under the person's own file name, so their web address can hold
 * characters an address cannot ("Course Flyer C# 100%.png"): the # cuts the address short and the picture never loads. This
 * writes those characters the way a browser needs them, without changing anything else, so older uploads show too.
 * Safe to call on any address: already-clean ones come back unchanged.
 */
export function webSafeUrl(url?: string | null): string {
  if (!url) return "";
  const match = /^(https?:\/\/[^/]+)(\/.*)$/i.exec(url);
  if (!match) return url;
  const path = match[2]
    .replace(/%(?![0-9A-Fa-f]{2})/g, "%25")
    .replace(/#/g, "%23")
    .replace(/\?/g, "%3F")
    .replace(/ /g, "%20");
  return match[1] + path;
}
