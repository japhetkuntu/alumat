"use client";

import { useEffect, useRef, useState } from "react";
import { Search } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

interface SearchModalProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  title?: string;
  children?: React.ReactNode;
  className?: string;
  triggerClassName?: string;
}

export function SearchModal({
  value,
  onChange,
  placeholder = "Search...",
  title = "Search",
  children,
  className,
  triggerClassName,
}: SearchModalProps) {
  const [open, setOpen] = useState(false);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (open) {
      inputRef.current?.focus();
    }
  }, [open]);

  return (
    <>
      <div className={cn("w-full", triggerClassName)}>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onFocus={() => setOpen(true)}
            placeholder={placeholder}
            className="pl-10"
          />
        </div>
      </div>

      <Dialog open={open} onOpenChange={(v) => setOpen(v)}>
        <DialogContent className={cn("max-w-3xl", className)}>
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
          </DialogHeader>

          <div className="mt-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input
                ref={inputRef}
                value={value}
                onChange={(e) => onChange(e.target.value)}
                placeholder={placeholder}
                className="pl-10"
              />
            </div>
            {children && <div className="mt-4 space-y-3">{children}</div>}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
