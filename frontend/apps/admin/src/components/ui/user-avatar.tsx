"use client";

import { useState } from "react";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/ui/avatar";
import { getInitials } from "@/lib/utils";

interface UserAvatarProps {
  src?: string | null;
  name: string;
  size?: "xs" | "sm" | "default" | "lg" | "xl";
  className?: string;
}

/**
 * Universal avatar component.
 * - If `src` is a valid URL → renders circular <img> with error fallback
 * - If `src` is null/empty or image fails to load → renders initials with deterministic bg colour
 * Use this everywhere a person is displayed.
 */
export function UserAvatar({ src, name, size = "default", className }: UserAvatarProps) {
  const [imgError, setImgError] = useState(false);
  const showImage = !!src && !imgError;

  return (
    <Avatar size={size} className={className}>
      {showImage ? (
        <AvatarImage
          src={src}
          alt={name}
          onError={() => setImgError(true)}
        />
      ) : null}
      {/* Always render fallback — it shows when image is absent or fails */}
      {!showImage && (
        <AvatarFallback name={name}>
          {getInitials(name)}
        </AvatarFallback>
      )}
    </Avatar>
  );
}
