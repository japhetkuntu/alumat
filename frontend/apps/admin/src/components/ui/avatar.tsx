import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const avatarVariants = cva(
  "relative flex shrink-0 overflow-hidden rounded-full",
  {
    variants: {
      size: {
        xs: "h-6 w-6 text-[10px]",
        sm: "h-8 w-8 text-[12px]",
        default: "h-10 w-10 text-[14px]",
        lg: "h-14 w-14 text-[16px]",
        xl: "h-20 w-20 text-[20px]",
      },
    },
    defaultVariants: { size: "default" },
  }
);

const Avatar = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement> & VariantProps<typeof avatarVariants>>(
  ({ className, size, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        avatarVariants({ size }),
        "border border-border/40 shadow-sm",
        className
      )}
      {...props}
    />
  )
);
Avatar.displayName = "Avatar";

const AvatarImage = React.forwardRef<HTMLImageElement, React.ImgHTMLAttributes<HTMLImageElement>>(
  ({ className, alt, ...props }, ref) => (
    <img ref={ref} alt={alt ?? "Avatar"} className={cn("aspect-square h-full w-full object-cover", className)} {...props} />
  )
);
AvatarImage.displayName = "AvatarImage";

const FALLBACK_COLORS = [
  "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  "bg-sky-500/10 text-sky-600 dark:text-sky-400",
  "bg-amber-500/10 text-amber-600 dark:text-amber-400",
  "bg-rose-500/10 text-rose-600 dark:text-rose-400",
  "bg-violet-500/10 text-violet-600 dark:text-violet-400",
  "bg-indigo-500/10 text-indigo-600 dark:text-indigo-400",
  "bg-cyan-500/10 text-cyan-600 dark:text-cyan-400",
  "bg-pink-500/10 text-pink-600 dark:text-pink-400",
  "bg-orange-500/10 text-orange-600 dark:text-orange-400",
  "bg-lime-500/10 text-lime-600 dark:text-lime-400",
];

function getColorFromName(name?: string) {
  if (!name) return FALLBACK_COLORS[0];
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
  return FALLBACK_COLORS[Math.abs(hash) % FALLBACK_COLORS.length];
}

const AvatarFallback = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement> & { name?: string }>(
  ({ className, name, children, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        "flex h-full w-full items-center justify-center rounded-full font-bold tracking-tight",
        getColorFromName(name ?? (typeof children === "string" ? children : undefined)),
        className
      )}
      {...props}
    >
      {children}
    </div>
  )
);
AvatarFallback.displayName = "AvatarFallback";

export { Avatar, AvatarImage, AvatarFallback };
