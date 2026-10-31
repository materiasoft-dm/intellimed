import { describe, expect, it } from 'vitest';
import type { PageDefinitionDto } from '@/api/types.gen';
import { buildTree, filterTree, KeySet, stateOf, toggle } from './permissionTree';

const page = (pageKey: string, category: string, group: string | null = null, action: string | null = null, description = ''): PageDefinitionDto =>
  ({ pageKey, pageName: pageKey, category, group, action, description });

const catalogue: PageDefinitionDto[] = [
  page('appointments', 'Appointments', 'Appointments', 'View'),
  page('appointments/create', 'Appointments', 'Appointments', 'New appointment'),
  page('appointments/delete', 'Appointments', 'Appointments', 'Delete appointment'),
  page('appointments/waiting-room', 'Appointments'),
  page('admin/users', 'Administration', null, null, 'Manage system users'),
];

describe('permission tree', () => {
  it('builds category → group → permission, keeping catalogue order', () => {
    const tree = buildTree(catalogue);
    expect(tree.map((r) => r.label)).toEqual(['Appointments', 'Administration']);
    const appointments = tree[0];
    expect(appointments.children.map((c) => c.label)).toEqual(['Appointments', 'appointments/waiting-room']);
    expect(appointments.children[0].children.map((c) => c.label)).toEqual(['View', 'New appointment', 'Delete appointment']);
    expect(appointments.leafKeys).toHaveLength(4);
  });

  it('a parent is all / some / none by what is granted beneath it', () => {
    const group = buildTree(catalogue)[0].children[0];
    expect(stateOf(group, new KeySet())).toBe('none');
    expect(stateOf(group, new KeySet(['appointments']))).toBe('some');
    expect(stateOf(group, new KeySet(['appointments', 'appointments/create', 'appointments/delete']))).toBe('all');
  });

  it('compares keys case-insensitively, like the server', () => {
    const group = buildTree(catalogue)[0].children[0];
    expect(stateOf(group, new KeySet(['APPOINTMENTS', 'Appointments/Create', 'appointments/DELETE']))).toBe('all');
  });

  it('ticking a parent grants everything beneath it, unticking revokes it', () => {
    const category = buildTree(catalogue)[0];
    const granted = toggle(category, new KeySet(['admin/users']), true);
    expect(granted.size).toBe(5);
    const revoked = toggle(category, granted, false);
    expect(revoked.values()).toEqual(['admin/users']);
  });

  it('search keeps matching leaves, their ancestors, and every leaf under a matching branch', () => {
    const tree = buildTree(catalogue);
    const byDescription = filterTree(tree, 'system users');
    expect(byDescription.map((r) => r.label)).toEqual(['Administration']);

    const byBranch = filterTree(tree, 'appointments');
    expect(byBranch[0].leafKeys).toHaveLength(4);

    const byLeaf = filterTree(tree, 'delete');
    const group = byLeaf[0].children[0];
    expect(group.children.map((c) => c.label)).toEqual(['Delete appointment']);
    // the parent's tick state still reflects the whole group, not just what the search shows
    expect(group.leafKeys).toHaveLength(3);
  });

  it('a blank search returns the tree unchanged', () => {
    const tree = buildTree(catalogue);
    expect(filterTree(tree, '  ')).toBe(tree);
  });
});
