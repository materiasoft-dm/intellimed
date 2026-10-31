// Port of IntelliMed.Core/Permissions/PermissionTree.cs — keep the two in step.
//
// The hierarchy is presentation-only: what is stored (and enforced) stays a flat set of page keys per role,
// so a parent never "has" a permission of its own — it is checked exactly when every permission beneath it
// is, and partially checked when only some are.

import type { PageDefinitionDto } from '@/api/types.gen';

export type CheckState = 'none' | 'some' | 'all';

export interface PermissionNode {
  id: string;
  label: string;
  description: string | null;
  /** The stored permission key; null for a branch. */
  pageKey: string | null;
  children: PermissionNode[];
  /** Every permission key at or beneath this node. A filtered copy keeps the full set, so a parent's tick
   *  state does not change while a search narrows what is visible. */
  leafKeys: string[];
}

/** A set of page keys compared case-insensitively (the server's keys are OrdinalIgnoreCase). */
export class KeySet {
  private readonly map = new Map<string, string>();
  constructor(keys: Iterable<string> = []) {
    for (const k of keys) this.add(k);
  }
  has(key: string) { return this.map.has(key.toLowerCase()); }
  add(key: string) { if (!this.has(key)) this.map.set(key.toLowerCase(), key); }
  delete(key: string) { this.map.delete(key.toLowerCase()); }
  values(): string[] { return [...this.map.values()]; }
  get size() { return this.map.size; }
  clone() { return new KeySet(this.values()); }
}

const leaf = (page: PageDefinitionDto, label: string): PermissionNode => ({
  id: page.pageKey,
  pageKey: page.pageKey,
  label,
  description: page.description?.trim() ? page.description : null,
  children: [],
  leafKeys: [page.pageKey],
});

const branch = (id: string, label: string, children: PermissionNode[]): PermissionNode => ({
  id, label, description: null, pageKey: null, children, leafKeys: children.flatMap((c) => c.leafKeys),
});

/** Insertion-ordered group-by: the order pages are declared in the catalogue is the order an admin sees. */
function groupBy<T>(items: T[], key: (t: T) => string): [string, T[]][] {
  const groups = new Map<string, T[]>();
  for (const item of items) {
    const k = key(item);
    const list = groups.get(k);
    if (list) list.push(item);
    else groups.set(k, [item]);
  }
  return [...groups.entries()];
}

/** Category → group → permission. Pages without a Group stand alone (keyed on their own page key). */
export function buildTree(pages: PageDefinitionDto[]): PermissionNode[] {
  return groupBy(pages, (p) => p.category).map(([category, inCategory]) => {
    const children = groupBy(inCategory, (p) => p.group ?? p.pageKey).map(([group, members]) =>
      members.length === 1
        ? leaf(members[0], members[0].pageName)
        : branch(`group:${category}/${group}`, group, members.map((m) => leaf(m, m.action ?? m.pageName))));
    return branch(`category:${category}`, category, children);
  });
}

export function stateOf(node: PermissionNode, selected: KeySet): CheckState {
  const keys = node.leafKeys;
  if (keys.length === 0) return 'none';
  const granted = keys.filter((k) => selected.has(k)).length;
  if (granted === 0) return 'none';
  return granted === keys.length ? 'all' : 'some';
}

/** Ticking a parent grants everything under it; unticking revokes everything under it. Returns a new set. */
export function toggle(node: PermissionNode, selected: KeySet, isChecked: boolean): KeySet {
  const next = selected.clone();
  for (const key of node.leafKeys) {
    if (isChecked) next.add(key);
    else next.delete(key);
  }
  return next;
}

const matches = (text: string | null | undefined, term: string) => !!text && text.toLowerCase().includes(term.toLowerCase());

/**
 * Narrows the tree to what matches `term`. A leaf matches on its own text or its ancestors' (searching
 * "appointments" shows every appointment permission); a branch is kept when anything beneath it is.
 */
export function filterTree(roots: PermissionNode[], term: string | null | undefined): PermissionNode[] {
  if (!term?.trim()) return roots;
  const needle = term.trim();
  const visit = (node: PermissionNode, ancestorMatched: boolean): PermissionNode | null => {
    const self = matches(node.label, needle) || matches(node.description, needle) || (node.pageKey != null && matches(node.pageKey, needle));
    const matched = ancestorMatched || self;
    if (node.pageKey != null) return matched ? node : null;
    const kept = node.children.map((c) => visit(c, matched)).filter((c): c is PermissionNode => c !== null);
    if (kept.length === 0) return null;
    return { ...node, children: kept, leafKeys: node.leafKeys };
  };
  return roots.map((r) => visit(r, false)).filter((r): r is PermissionNode => r !== null);
}
