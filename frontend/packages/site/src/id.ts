const A = "abcdefghijklmnopqrstuvwxyz0123456789";
/** A short identifier for a section or an item. Letters and digits only, which is what the server accepts. */
export function uid(): string {
  const bytes = new Uint8Array(10);
  if (typeof crypto !== "undefined" && crypto.getRandomValues) crypto.getRandomValues(bytes);
  else for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256);
  return "s" + Array.from(bytes, (b) => A[b % A.length]).join("");
}
