import * as React from "react";
import { AlertCircle } from "./icons";
import { Button } from "./button";
import { EmptyState } from "./empty-state";

interface LoadErrorProps {
  title?: string;
  description?: string;
  /** Re-runs the failed request. Omit only when retrying can't help. */
  onRetry?: () => void;
  className?: string;
}

/**
 * What a list shows when its data could not be loaded. Kept separate from
 * EmptyState on purpose: "nothing here yet" and "we couldn't check" are
 * different facts, and a failed request must never read as an empty list.
 */
export function LoadError({
  title = "We couldn't load this",
  description = "Check your connection, then try again.",
  onRetry,
  className,
}: LoadErrorProps) {
  return (
    <EmptyState
      className={className}
      icon={<AlertCircle size={40} />}
      title={title}
      description={description}
      action={onRetry ? <Button variant="outline" size="sm" className="font-semibold" onClick={onRetry}>Try again</Button> : undefined}
    />
  );
}
