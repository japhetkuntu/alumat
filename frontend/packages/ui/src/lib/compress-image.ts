/**
 * Shrinks a photo in the browser before it is uploaded. Phone cameras produce 4-12 MB files at 4000+ pixels wide, far
 * more than any screen here shows; sending that over a mobile connection is the slow part of every upload and it
 * ties up the API for as long as the bytes are arriving. Resizing to a sensible maximum and re-encoding typically
 * cuts a photo by 80-95% with no visible difference.
 *
 * The file keeps its format and name. Always safe to call: anything that is not a resizable photo (GIFs, SVGs, small
 * files, anything the browser can't decode) comes back untouched, and so does a file the re-encode failed to make
 * meaningfully smaller.
 */
export interface CompressImageOptions {
  /** Longest side in pixels after resizing. Smaller images are never enlarged. */
  maxDimension?: number;
  /** 0 to 1. */
  quality?: number;
  /** Files at or under this size are left alone. */
  minBytes?: number;
}

const RESIZABLE = new Set(["image/jpeg", "image/png", "image/webp"]);

function canvasToBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality));
}

export async function compressImage(file: File, options: CompressImageOptions = {}): Promise<File> {
  const { maxDimension = 1600, quality = 0.82, minBytes = 200 * 1024 } = options;
  try {
    if (typeof document === "undefined" || typeof createImageBitmap === "undefined") return file;
    if (!RESIZABLE.has(file.type) || file.size <= minBytes) return file;

    // "from-image" applies the photo's rotation (phones store portrait shots sideways plus an orientation flag).
    const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
    const scale = Math.min(1, maxDimension / Math.max(bitmap.width, bitmap.height));
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) { bitmap.close(); return file; }
    // JPEG has no transparency, so a white base keeps see-through areas from turning black. PNG and WebP keep theirs.
    if (file.type === "image/jpeg") { context.fillStyle = "#ffffff"; context.fillRect(0, 0, width, height); }
    context.drawImage(bitmap, 0, 0, width, height);
    bitmap.close();

    // The file keeps its own format: logos end up in emails and on pages that cannot all show WebP, so a PNG stays a PNG.
    const blob = await canvasToBlob(canvas, file.type, quality);
    // A browser that cannot write this format hands back something else; and a result that barely shrank is not worth swapping in.
    if (!blob || blob.type !== file.type || blob.size > file.size * 0.9) return file;
    return new File([blob], file.name, { type: file.type, lastModified: Date.now() });
  } catch {
    return file;
  }
}

/** Compresses several files at once, keeping their order. */
export function compressImages(files: File[], options?: CompressImageOptions): Promise<File[]> {
  return Promise.all(files.map((f) => compressImage(f, options)));
}
