"use client";

import { useState } from "react";
import { Button } from "./button";
import { Input } from "./input";
import { Label } from "./label";
import { ImageUpload } from "./image-upload";

export interface LinkOrUploadProps {
  label: string;
  /** Current URL — either what was typed as a link, or the URL of a file already uploaded. */
  url: string;
  onUrlChange: (value: string) => void;
  /** A file picked but not yet uploaded — the caller uploads it (e.g. on form submit) and calls onUrlChange with the result. */
  file: File | null;
  onFileChange: (file: File | null) => void;
  accept?: string;
  placeholder?: string;
  helperText?: string;
  className?: string;
}

/**
 * A field that can be filled either by pasting a link or by uploading a
 * file, instead of forcing admins to have a URL on hand already. Both modes
 * write to the same `url`/`file` pair — the caller decides at save time:
 * if `file` is set, upload it and use the returned URL; otherwise use `url`
 * as typed. Switching to "Paste a link" clears any pending file so the two
 * never disagree about which one wins.
 */
export function LinkOrUpload({ label, url, onUrlChange, file, onFileChange, accept, placeholder = "https://...", helperText, className }: LinkOrUploadProps) {
  const [mode, setMode] = useState<"link" | "upload">(file ? "upload" : "link");

  return (
    <div className={className ? `space-y-2 ${className}` : "space-y-2"}>
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <Label>{label}</Label>
        <div className="flex gap-1.5">
          <Button type="button" size="sm" variant={mode === "link" ? "default" : "outline"}
            onClick={() => { setMode("link"); onFileChange(null); }}>
            Paste a link
          </Button>
          <Button type="button" size="sm" variant={mode === "upload" ? "default" : "outline"}
            onClick={() => setMode("upload")}>
            Upload a file
          </Button>
        </div>
      </div>
      {mode === "link" ? (
        <Input type="url" placeholder={placeholder} value={url} onChange={(e) => onUrlChange(e.target.value)} />
      ) : (
        <ImageUpload file={file} existingUrl={url} onChange={onFileChange} onClearExisting={() => onUrlChange("")} accept={accept} label={label} />
      )}
      {helperText && <p className="text-xs text-muted-foreground">{helperText}</p>}
    </div>
  );
}
