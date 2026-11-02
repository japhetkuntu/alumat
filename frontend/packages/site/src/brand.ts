/** A small shield with the first letter of the name, drawn in the brand colour. Used when an institution has not uploaded a logo. */
export function crestUrl(color: string, letter: string): string {
  const safe = (letter || "A").slice(0, 1).toUpperCase().replace(/[^A-Z0-9]/g, "A");
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><path d="M32 3 56 11v20c0 15-10 26-24 30C18 57 8 46 8 31V11z" fill="${color}"/><path d="M32 8.5 51 15v16c0 12-8 21-19 25-11-4-19-13-19-25V15z" fill="none" stroke="#fff" stroke-opacity=".55" stroke-width="2"/><text x="32" y="42" text-anchor="middle" font-family="Georgia,serif" font-weight="700" font-size="26" fill="#fff">${safe}</text></svg>`;
  return "data:image/svg+xml," + encodeURIComponent(svg);
}
