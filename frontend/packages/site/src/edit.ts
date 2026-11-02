import type { SiteSection } from "./types";

/**
 * What the editor gives the page so it can be changed in place. The visitor's page never has one: every part that can be edited renders
 * as plain text and plain photos when this is missing, so the same components serve both.
 */
export interface EditApi {
  selectedId: string | null;
  select(id: string | null): void;
  setProp(sectionId: string, key: string, value: string): void;
  setItemField(sectionId: string, itemId: string, key: string, value: string): void;
  pickPhoto(sectionId: string, slot: string): void;
  removePhoto(sectionId: string, slot: string): void;
  addItem(sectionId: string): void;
  removeItem(sectionId: string, itemId: string): void;
  insertAt(index: number): void;
  move(sectionId: string, dir: -1 | 1): void;
  duplicate(sectionId: string): void;
  remove(sectionId: string): void;
  /** Shows where a button goes, and lets the editor open its settings. */
  describeLink(section: SiteSection, key: string, itemId?: string): string;
}
