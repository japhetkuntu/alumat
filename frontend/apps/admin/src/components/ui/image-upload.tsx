"use client";

import { useRef, useMemo, useState, useEffect, useCallback } from "react";
import { Upload, X, FileIcon, ImageIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

interface ImageUploadProps {
  file?: File | null;
  existingUrl?: string;
  onChange: (file: File | null) => void;
  onClearExisting?: () => void;
  label?: string;
  accept?: string;
  className?: string;
}

export function ImageUpload({ file, existingUrl, onChange, onClearExisting, label = "Upload image", accept = "image/*", className }: ImageUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [imgError, setImgError] = useState(false);

  const isImage = accept === "image/*" || accept?.startsWith("image");
  const previewUrl = useMemo(() => {
    if (file && isImage && file.type.startsWith("image/")) return URL.createObjectURL(file);
    return null;
  }, [file, isImage]);

  // Cleanup objectURL on unmount or when preview changes
  useEffect(() => {
    return () => { if (previewUrl) URL.revokeObjectURL(previewUrl); };
  }, [previewUrl]);

  const imageUrl = previewUrl || (isImage && existingUrl ? existingUrl : null);
  const displayValue = file ? file.name : existingUrl;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (selected) { onChange(selected); setImgError(false); }
    if (inputRef.current) inputRef.current.value = "";
  };

  const handleClear = () => {
    onChange(null);
    setImgError(false);
    if (existingUrl) onClearExisting?.();
  };

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const droppedFile = e.dataTransfer.files?.[0];
    if (droppedFile) { onChange(droppedFile); setImgError(false); }
  }, [onChange]);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
  }, []);

  return (
    <div className={cn("space-y-2", className)}>
      <input ref={inputRef} type="file" accept={accept} className="hidden" onChange={handleFileChange} />
      {displayValue ? (
        <div className="relative group">
          {imageUrl && !imgError ? (
            <div className="relative rounded-xl overflow-hidden border bg-muted/30">
              <div className="aspect-[16/9]">
                <img
                  src={imageUrl}
                  alt="Upload preview"
                  className="w-full h-full object-cover"
                  loading="lazy"
                  onError={() => setImgError(true)}
                />
              </div>
              <div className="absolute inset-0 bg-black/0 group-hover:bg-black/40 transition-colors duration-150 flex items-center justify-center">
                <div className="opacity-0 group-hover:opacity-100 transition-opacity duration-150 flex gap-2">
                  <Button type="button" size="sm" variant="secondary" onClick={() => inputRef.current?.click()}>
                    Change
                  </Button>
                  <Button type="button" size="sm" variant="destructive" onClick={handleClear}>
                    <X size={12} /> Remove
                  </Button>
                </div>
              </div>
              {file && (
                <div className="absolute bottom-0 left-0 right-0 bg-black/50 backdrop-blur-sm text-white text-xs px-3 py-1.5 flex items-center justify-between">
                  <span className="truncate">{file.name}</span>
                  <span className="text-white/70 shrink-0 ml-2">{formatFileSize(file.size)}</span>
                </div>
              )}
            </div>
          ) : (
            <div className="flex items-center gap-3 p-3 border rounded-xl bg-muted/30">
              <div className="h-9 w-9 rounded-lg bg-muted flex items-center justify-center shrink-0">
                <FileIcon size={16} className="text-muted-foreground" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm truncate">{file?.name ?? (typeof existingUrl === "string" ? existingUrl.split("/").pop() : "File")}</p>
                {file && <p className="text-xs text-muted-foreground">{formatFileSize(file.size)}</p>}
              </div>
              <Button type="button" size="icon" variant="ghost" className="h-7 w-7 shrink-0" onClick={handleClear}>
                <X size={14} />
              </Button>
            </div>
          )}
        </div>
      ) : (
        <div
          className={cn(
            "relative rounded-xl border-2 border-dashed transition-all duration-150 cursor-pointer",
            dragOver
              ? "border-primary bg-primary/5 scale-[1.01]"
              : "border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/40"
          )}
          onClick={() => inputRef.current?.click()}
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
        >
          <div className="flex flex-col items-center justify-center py-8 gap-2 pointer-events-none">
            <div className={cn(
              "h-10 w-10 rounded-xl flex items-center justify-center transition-colors duration-150",
              dragOver ? "bg-primary/10 text-primary" : "bg-muted text-muted-foreground"
            )}>
              {isImage ? <ImageIcon size={18} /> : <Upload size={18} />}
            </div>
            <div className="text-center">
              <p className="text-sm font-medium">{dragOver ? "Drop file here" : label}</p>
              <p className="text-xs text-muted-foreground mt-0.5">or click to browse</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
