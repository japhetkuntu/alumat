"use client";

import * as React from "react";
import { generateBrandPalette } from "../lib/brand-palette";

export interface BrandPreviewProps {
  /** Hex color the user is currently considering — updates live as they type/pick. */
  color: string;
  /** Optional second brand color — many institutions have a real two-color identity. Falls back to the platform's generic gold accent when omitted. */
  secondaryColor?: string;
  /** Institution/portal name shown in the mockups. Defaults to a generic placeholder. */
  name?: string;
  className?: string;
}

/**
 * Shows how a candidate brand color will actually look across the
 * Institution Portal, Member Portal, and outbound email — using the exact
 * same `generateBrandPalette` derivation that theme.ts and the email
 * renderer use, so this preview is the real result, not an approximation
 * of it. Pure client-side computation; nothing is sent anywhere or saved
 * until the caller's own save action runs.
 */
export function BrandPreview({ color, secondaryColor, name = "Your Portal", className }: BrandPreviewProps) {
  const palette = React.useMemo(() => {
    try {
      return generateBrandPalette(color, secondaryColor || undefined);
    } catch {
      return null;
    }
  }, [color, secondaryColor]);

  if (!palette) {
    return (
      <div className={className} style={{ fontSize: 13, color: "#94a3b8", padding: 16 }}>
        Enter a color to see a preview.
      </div>
    );
  }

  const initial = (name.trim()[0] ?? "A").toUpperCase();

  return (
    <div className={className} style={{ display: "grid", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <InstitutionPortalMock palette={palette} name={name} initial={initial} />
        <MemberPortalMock palette={palette} name={name} initial={initial} />
      </div>
      <EmailMock palette={palette} name={name} initial={initial} />
    </div>
  );
}

interface MockProps {
  palette: ReturnType<typeof generateBrandPalette>;
  name: string;
  initial: string;
}

function PreviewFrame({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ borderRadius: 12, overflow: "hidden", border: "1px solid #e2e8f0", background: "#fff" }}>
      <div style={{ fontSize: 10.5, fontWeight: 700, letterSpacing: 0.4, textTransform: "uppercase", color: "#94a3b8", padding: "8px 12px", borderBottom: "1px solid #f1f5f9" }}>
        {label}
      </div>
      {children}
    </div>
  );
}

function InstitutionPortalMock({ palette, name, initial }: MockProps) {
  return (
    <PreviewFrame label="Institution Portal">
      <div style={{ display: "flex", height: 150, fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" }}>
        <div style={{ width: 54, background: "#0f172a", display: "flex", flexDirection: "column", alignItems: "center", paddingTop: 12, gap: 8 }}>
          <div style={{ width: 24, height: 24, borderRadius: 6, background: palette.primary, color: palette.textOnPrimary, fontSize: 11, fontWeight: 800, display: "flex", alignItems: "center", justifyContent: "center" }}>
            {initial}
          </div>
          <div style={{ width: 30, height: 6, borderRadius: 3, background: palette.primaryDark }} />
          <div style={{ width: 30, height: 6, borderRadius: 3, background: "#1e293b" }} />
          <div style={{ width: 30, height: 6, borderRadius: 3, background: "#1e293b" }} />
        </div>
        <div style={{ flex: 1, padding: 12, display: "flex", flexDirection: "column", gap: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: "#0f172a" }}>{name}</div>
          <div style={{ borderRadius: 8, background: palette.primaryLight, border: `1px solid ${palette.primarySoft}`, padding: 8, fontSize: 9.5, color: palette.primaryDark }}>
            Info callout uses your color
          </div>
          <div style={{ display: "flex", gap: 6 }}>
            <button
              type="button"
              style={{ border: "none", borderRadius: 7, padding: "6px 12px", fontSize: 10.5, fontWeight: 700, background: palette.primary, color: palette.textOnPrimary, cursor: "default" }}
            >
              Primary action
            </button>
            {palette.accent && (
              <span style={{ alignSelf: "center", borderRadius: 999, padding: "3px 9px", fontSize: 9.5, fontWeight: 700, background: palette.accent.light, color: palette.accent.dark }}>
                Featured
              </span>
            )}
          </div>
        </div>
      </div>
    </PreviewFrame>
  );
}

function MemberPortalMock({ palette, name, initial }: MockProps) {
  return (
    <PreviewFrame label="Member Portal">
      <div style={{ height: 150, fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif", padding: 12, display: "flex", flexDirection: "column", gap: 8 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <div style={{ width: 22, height: 22, borderRadius: 6, background: `linear-gradient(135deg, ${palette.primary}, ${palette.primaryDark})`, color: palette.textOnPrimary, fontSize: 10, fontWeight: 800, display: "flex", alignItems: "center", justifyContent: "center" }}>
            {initial}
          </div>
          <div style={{ fontSize: 11, fontWeight: 700, color: "#0f172a" }}>{name}</div>
        </div>
        <div style={{ display: "flex", gap: 6 }}>
          <span style={{ borderRadius: 999, padding: "3px 9px", fontSize: 9.5, fontWeight: 700, background: palette.primaryLight, color: palette.primaryDark }}>Verified</span>
          {palette.accent ? (
            <span style={{ borderRadius: 999, padding: "3px 9px", fontSize: 9.5, fontWeight: 700, background: palette.accent.light, color: palette.accent.dark }}>Top donor</span>
          ) : (
            <span style={{ borderRadius: 999, padding: "3px 9px", fontSize: 9.5, fontWeight: 700, background: "#f1f5f9", color: "#64748b" }}>Alumni</span>
          )}
        </div>
        <div style={{ borderRadius: 10, border: "1px solid #eef2f1", padding: 10, fontSize: 9.5, color: "#475569", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span>New job posted in your network</span>
          <button
            type="button"
            style={{ border: "none", borderRadius: 6, padding: "5px 10px", fontSize: 9.5, fontWeight: 700, background: palette.primary, color: palette.textOnPrimary, cursor: "default" }}
          >
            View
          </button>
        </div>
      </div>
    </PreviewFrame>
  );
}

function EmailMock({ palette, name, initial }: MockProps) {
  return (
    <PreviewFrame label="Outbound email">
      <div style={{ background: "#f1f5f4", padding: 20 }}>
        <div style={{ height: 4, borderRadius: 2, background: `linear-gradient(90deg, ${palette.primary}, ${palette.primaryDark})`, marginBottom: 14 }} />
        <div style={{ background: "#fff", borderRadius: 12, padding: 20, fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif", boxShadow: "0 8px 20px rgba(15,23,42,0.06)" }}>
          <div style={{ width: 28, height: 28, borderRadius: 8, background: `linear-gradient(135deg, ${palette.primary}, ${palette.primaryDark})`, color: palette.textOnPrimary, fontSize: 12, fontWeight: 800, display: "flex", alignItems: "center", justifyContent: "center", marginBottom: 10 }}>
            {initial}
          </div>
          <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: 1, textTransform: "uppercase", color: palette.primaryDark, marginBottom: 10 }}>{name}</div>
          <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a", marginBottom: 8 }}>Verify your email</div>
          <div style={{ fontSize: 11, color: "#475569", marginBottom: 12, lineHeight: 1.5 }}>Use the code below to complete your registration.</div>
          <div style={{ textAlign: "center", background: palette.primaryLight, border: `1px solid ${palette.primarySoft}`, borderRadius: 10, padding: "10px 0", fontSize: 16, fontWeight: 800, letterSpacing: 4, color: palette.primaryDark }}>
            482913
          </div>
        </div>
      </div>
    </PreviewFrame>
  );
}
