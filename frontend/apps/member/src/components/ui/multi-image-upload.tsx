"use client";

import { useRef, useMemo, useState, useEffect, useCallback } from "react";
import { X, Plus, ImageIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface MultiImageUploadProps {
  files: File[];
  existingUrls: string[];
  onAddFile: (file: File) => void;
  onRemoveFile: (index: number) => void;
  onRemoveExisting: (index: number) => void;
  label?: string;
  accept?: string;
  className?: string;
}

export function MultiImageUpload({
  files,
  existingUrls,
  onAddFile,
  onRemoveFile,
  onRemoveExisting,
  label = "Add images",
  accept = "image/*",
  className,
}: MultiImageUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);

  const filePreviews = useMemo(() =>
    files.map((f) => f.type.startsWith("image/") ? URL.createObjectURL(f) : null),
    [files]
  );

  // Cleanup objectURLs on unmount or when files change
  useEffect(() => {
    return () => { filePreviews.forEach((url) => { if (url) URL.revokeObjectURL(url); }); };
  }, [filePreviews]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files;
    if (selected) Array.from(selected).forEach((f) => onAddFile(f));
    if (inputRef.current) inputRef.current.value = "";
  };

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const dropped = e.dataTransfer.files;
    if (dropped) Array.from(dropped).forEach((f) => onAddFile(f));
  }, [onAddFile]);

  const handleDragOver = useCallback((e: React.DragEvent) => { e.preventDefault(); setDragOver(true); }, []);
  const handleDragLeave = useCallback((e: React.DragEvent) => { e.preventDefault(); setDragOver(false); }, []);

  const hasItems = existingUrls.length > 0 || files.length > 0;
  const totalCount = existingUrls.length + files.length;

  return (
    <div className={cn("space-y-2", className)}>
      <input ref={inputRef} type="file" accept={accept} className="hidden" multiple onChange={handleFileChange} />

      {hasItems && (
        <div>
          {totalCount > 0 && (
            <p className="text-xs text-muted-foreground mb-2">{totalCount} {totalCount === 1 ? "file" : "files"}</p>
          )}
          <div
            className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 gap-2"
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
          >
            {existingUrls.map((url, i) => (
              <div key={`existing-${i}`} className="relative group aspect-square rounded-xl overflow-hidden border bg-muted/30">
                <img src={url} alt="" className="w-full h-full object-cover" loading="lazy" />
                <button
                  type="button"
                  className="absolute top-1.5 right-1.5 h-6 w-6 rounded-full bg-black/60 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-150"
                  onClick={() => onRemoveExisting(i)}
                  aria-label="Remove image"
                >
                  <X size={12} />
                </button>
              </div>
            ))}
            {files.map((f, i) => (
              <div key={`new-${i}`} className="relative group aspect-square rounded-xl overflow-hidden ring-2 ring-primary/40 bg-primary/5">
                {filePreviews[i] ? (
                  <img src={filePreviews[i]!} alt="" className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-xs text-muted-foreground p-2 text-center break-all">
                    {f.name}
                  </div>
                )}
                <button
                  type="button"
                  className="absolute top-1.5 right-1.5 h-6 w-6 rounded-full bg-black/60 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-150"
                  onClick={() => onRemoveFile(i)}
                  aria-label="Remove image"
                >
                  <X size={12} />
                </button>
                <div className="absolute bottom-0 left-0 right-0 bg-primary/80 text-white text-[10px] px-1.5 py-0.5 truncate font-medium">
                  New
                </div>
              </div>
            ))}
            <button
              type="button"
              className={cn(
                "aspect-square rounded-xl border-2 border-dashed flex flex-col items-center justify-center gap-1 transition-all duration-150 cursor-pointer",
                dragOver
                  ? "border-primary bg-primary/5 scale-[1.02]"
                  : "border-muted-foreground/25 text-muted-foreground hover:border-primary hover:text-primary hover:bg-primary/5"
              )}
              onClick={() => inputRef.current?.click()}
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
            >
              <Plus size={18} />
              <span className="text-[10px] font-medium">Add</span>
            </button>
          </div>
        </div>
      )}

      {!hasItems && (
        <div
          className={cn(
            "rounded-xl border-2 border-dashed transition-all duration-150 cursor-pointer",
            dragOver
              ? "border-primary bg-primary/5 scale-[1.01]"
              : "border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/40"
          )}
          onClick={() => inputRef.current?.click()}
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
        >
          <div className="flex flex-col items-center justify-center py-6 gap-2 pointer-events-none">
            <div className={cn(
              "h-9 w-9 rounded-xl flex items-center justify-center transition-colors duration-150",
              dragOver ? "bg-primary/10 text-primary" : "bg-muted text-muted-foreground"
            )}>
              <ImageIcon size={16} />
            </div>
            <div className="text-center">
              <p className="text-sm font-medium">{dragOver ? "Drop files here" : label}</p>
              <p className="text-xs text-muted-foreground mt-0.5">or click to browse</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
