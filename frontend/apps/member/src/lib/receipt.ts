/**
 * A payment receipt a member can print or save as a PDF. Built entirely from what the member's own
 * contribution history already carries, so there is nothing new to fetch and no file to store. It shows
 * what the member paid and to whom; platform fees and settlement figures are never part of it.
 */
export interface ReceiptData {
  institutionName: string;
  logoUrl?: string | null;
  colour?: string | null;
  memberName: string;
  memberNumber?: string | null;
  memberEmail?: string | null;
  campaignTitle: string;
  /** Already formatted for display, e.g. "GH₵150.00". */
  amount: string;
  method: string;
  /** The payment reference from the processor, when there is one. */
  reference?: string | null;
  /** Our own id for the payment; shortened into the receipt number. */
  paymentId: string;
  /** Already formatted for display. */
  paidOn: string;
}

function esc(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** A hex colour or the default; never raw user text inside a style attribute. */
function safeColour(value?: string | null): string {
  return value && /^#[0-9a-fA-F]{6}$/.test(value) ? value : "#0e7143";
}

export function receiptNumber(paymentId: string): string {
  return `R-${paymentId.replace(/[^a-zA-Z0-9]/g, "").slice(0, 8).toUpperCase()}`;
}

export function buildReceiptHtml(r: ReceiptData): string {
  const colour = safeColour(r.colour);
  const row = (label: string, value?: string | null) =>
    value ? `<tr><th>${esc(label)}</th><td>${esc(value)}</td></tr>` : "";
  const logo = r.logoUrl && /^https?:\/\//i.test(r.logoUrl)
    ? `<img src="${esc(r.logoUrl)}" alt="" width="48" height="48" style="object-fit:cover" />`
    : `<div class="mark">${esc((r.institutionName.trim()[0] ?? "R").toUpperCase())}</div>`;

  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Receipt ${esc(receiptNumber(r.paymentId))}</title>
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>
  *{box-sizing:border-box}
  body{margin:0;padding:32px 16px;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;color:#1a1a1a;background:#f4f4f2}
  .sheet{max-width:560px;margin:0 auto;background:#fff;border:1px solid #e3e3df;padding:32px}
  header{display:flex;align-items:center;gap:14px;border-bottom:3px solid ${colour};padding-bottom:18px}
  .mark{width:48px;height:48px;background:${colour};color:#fff;font-weight:800;font-size:22px;display:flex;align-items:center;justify-content:center}
  h1{margin:0;font-size:18px} .sub{margin:2px 0 0;font-size:12px;color:#666;letter-spacing:.08em;text-transform:uppercase}
  .amount{margin:26px 0 4px;font-size:34px;font-weight:800;color:${colour}}
  .paid{margin:0 0 22px;font-size:13px;color:#555}
  table{width:100%;border-collapse:collapse;font-size:14px}
  th{text-align:left;font-weight:600;color:#666;padding:9px 0;border-top:1px solid #eee;width:38%;vertical-align:top}
  td{padding:9px 0;border-top:1px solid #eee;word-break:break-word}
  footer{margin-top:24px;font-size:12px;color:#777;line-height:1.5}
  .actions{max-width:560px;margin:16px auto 0;text-align:center}
  button{font:inherit;font-weight:600;padding:10px 18px;border:0;background:${colour};color:#fff;cursor:pointer}
  @media print{body{background:#fff;padding:0}.sheet{border:0;padding:0}.actions{display:none}}
</style></head><body>
<div class="sheet">
  <header>${logo}<div><h1>${esc(r.institutionName)}</h1><p class="sub">Payment receipt</p></div></header>
  <p class="amount">${esc(r.amount)}</p>
  <p class="paid">Paid on ${esc(r.paidOn)}</p>
  <table>
    ${row("Receipt number", receiptNumber(r.paymentId))}
    ${row("For", r.campaignTitle)}
    ${row("Received from", r.memberName)}
    ${row("Member number", r.memberNumber)}
    ${row("Email", r.memberEmail)}
    ${row("Payment method", r.method)}
    ${row("Reference", r.reference)}
  </table>
  <footer>This receipt confirms a payment recorded by ${esc(r.institutionName)} on AlumUnion. Keep it for your records.</footer>
</div>
<div class="actions"><button onclick="window.print()">Print or save as PDF</button></div>
</body></html>`;
}

/** Opens the receipt in its own window, ready to print. Returns false if the browser blocked the window. */
export function openReceipt(data: ReceiptData): boolean {
  const win = window.open("", "_blank", "noopener=no,width=720,height=900");
  if (!win) return false;
  win.document.open();
  win.document.write(buildReceiptHtml(data));
  win.document.close();
  win.focus();
  return true;
}
