import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { AdminApi } from '@/api/admin';
import type { PageDefinitionDto } from '@/api/types.gen';
import { LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { buildTree, filterTree, KeySet, stateOf, toggle, type PermissionNode } from '@/lib/permissionTree';

/**
 * One row of the tree, recursing into its children. A branch (category or group) has a tri-state checkbox —
 * ticked when everything under it is granted, partly ticked when only some is; ticking it grants or revokes
 * everything under it. A leaf is a single stored permission.
 */
function TreeNode({ node, depth, selected, collapsed, onToggle, onExpandToggle }: {
  node: PermissionNode;
  depth: number;
  selected: KeySet;
  collapsed: Set<string>;
  onToggle: (node: PermissionNode, checked: boolean) => void;
  onExpandToggle: (id: string) => void;
}) {
  const state = stateOf(node, selected);
  const inputId = `chk_${node.id}`;
  const indent = { paddingLeft: `${0.75 + depth * 1.75}rem` };

  if (node.pageKey != null) {
    return (
      <li className="perm-node" data-node={node.id}>
        <div className="perm-row perm-leaf" style={indent}>
          <span className="perm-chevron-spacer" />
          <input type="checkbox" className="form-check-input" id={inputId} data-tri={state === 'all' ? 'on' : 'off'}
                 checked={state === 'all'} onChange={(e) => onToggle(node, e.target.checked)} />
          <label className="perm-label" htmlFor={inputId}>{node.label}</label>
          {node.description && <span className="perm-desc">{node.description}</span>}
        </div>
      </li>
    );
  }

  const expanded = !collapsed.has(node.id);
  const granted = node.leafKeys.filter((k) => selected.has(k)).length;
  return (
    <li className="perm-node" data-node={node.id}>
      <div className={`perm-row perm-branch perm-depth-${depth}`} style={indent}>
        <button type="button" className="perm-chevron" aria-expanded={expanded} aria-label={`${expanded ? 'Collapse' : 'Expand'} ${node.label}`}
                onClick={() => onExpandToggle(node.id)}>
          <i className={`bi ${expanded ? 'bi-chevron-down' : 'bi-chevron-right'}`} />
        </button>
        <input type="checkbox" className="form-check-input" id={inputId}
               data-tri={state === 'all' ? 'on' : state === 'some' ? 'mixed' : 'off'}
               checked={state === 'all'}
               // "partly ticked" is the DOM's indeterminate property — there is no attribute for it
               ref={(el) => { if (el) el.indeterminate = state === 'some'; }}
               onChange={(e) => onToggle(node, e.target.checked)} />
        <label className="perm-label" htmlFor={inputId}>{node.label}</label>
        <span className="perm-count">{granted} of {node.leafKeys.length}</span>
      </div>
      {expanded && (
        <ul className="perm-children">
          {node.children.map((child) => (
            <TreeNode key={child.id} node={child} depth={depth + 1} selected={selected} collapsed={collapsed}
                      onToggle={onToggle} onExpandToggle={onExpandToggle} />
          ))}
        </ul>
      )}
    </li>
  );
}

export default function RoleConfiguration() {
  usePageTitle('Role Configuration - IntelliMed');
  const navigate = useNavigate();
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [roles, setRoles] = useState<string[]>([]);
  const [allPages, setAllPages] = useState<PageDefinitionDto[]>([]);
  const [selectedRole, setSelectedRole] = useState<string | null>(null);
  const [selected, setSelected] = useState(new KeySet());
  const [search, setSearch] = useState('');
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);
  const [loadingRole, setLoadingRole] = useState(false);

  useMountEffect(async (alive) => {
    try {
      const [r, pages] = await Promise.all([AdminApi.getRoles(), AdminApi.getPageDefinitions()]);
      if (!alive()) return;
      setRoles((r ?? []).map((x) => x.name).sort());
      setAllPages(pages ?? []);
    } catch (e) {
      setError(`Failed to load data: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setLoading(false);
    }
  });

  const tree = useMemo(() => buildTree(allPages), [allPages]);
  const visibleTree = useMemo(() => filterTree(tree, search), [tree, search]);
  const selectedCount = allPages.filter((p) => selected.has(p.pageKey)).length;

  const chooseRole = async (role: string) => {
    setSelectedRole(role || null);
    setSelected(new KeySet());
    if (!role) return;
    setLoadingRole(true);
    try {
      const perms = await AdminApi.getRolePermissions(role);
      setSelected(new KeySet(perms?.pageKeys ?? []));
    } catch (e) {
      toast.error(`Failed to load permissions for ${role}: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setLoadingRole(false);
    }
  };

  const onExpandToggle = (id: string) => setCollapsed((c) => {
    const next = new Set(c);
    if (!next.delete(id)) next.add(id);
    return next;
  });

  // collapse every branch, keeping the categories themselves open so the groups stay visible
  const collapseAll = () => setCollapsed(new Set(tree.flatMap((root) => root.children.filter((c) => c.pageKey == null).map((c) => c.id))));

  const save = async () => {
    if (!selectedRole) return;
    setSaving(true);
    try {
      const result = await AdminApi.saveRolePermissions(selectedRole, { pageKeys: selected.values() });
      if (result.success) toast.success(`Permissions saved for '${selectedRole}' (${selectedCount} pages).`);
      else toast.error(result.message ?? 'Failed to save permissions.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <h4 className="mb-4">Role Configuration</h4>
      <div className="card mb-4">
        <div className="card-body">
          <div className="d-flex align-items-center justify-content-between">
            <span className="d-flex align-items-center"><i className="bi bi-shield-lock me-2" /> Configure which pages each role can access</span>
            <button type="button" className="btn btn-outline-primary" onClick={() => navigate('/admin/users')}><i className="bi bi-arrow-left me-1" /> Back to Users</button>
          </div>
        </div>
      </div>

      {loading ? (
        <LoadingState text="Loading roles and pages..." compact={false} />
      ) : error ? (
        <div className="alert alert-danger mb-4">{error}</div>
      ) : (
        <>
          <div className="card mb-4">
            <div className="card-body">
              <div className="row g-3 align-items-end">
                <div className="col-md-5">
                  <label className="form-label">Select Role</label>
                  <select className="form-select" value={selectedRole ?? ''} onChange={(e) => chooseRole(e.target.value)}>
                    <option value="">-- Choose a role --</option>
                    {roles.map((r) => <option key={r} value={r}>{r}</option>)}
                  </select>
                </div>
                <div className="col-md-5">
                  <label className="form-label">Search pages...</label>
                  <div className="input-group">
                    <span className="input-group-text"><i className="bi bi-search" /></span>
                    <input type="text" className="form-control" placeholder="Search..." value={search} onChange={(e) => setSearch(e.target.value)} />
                  </div>
                </div>
                <div className="col-md-2">
                  <button type="button" className="btn btn-primary w-100" onClick={save} disabled={!selectedRole || saving}>
                    {saving ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />} Save Permissions
                  </button>
                </div>
              </div>
            </div>
          </div>

          {!selectedRole ? (
            <div className="card text-center py-5">
              <div className="card-body">
                <i className="bi bi-hand-index-thumb fs-1 text-muted mb-3 d-block" />
                <h5>Select a role above to configure its page access</h5>
              </div>
            </div>
          ) : (
            <>
              <div className="card mb-3">
                <div className="card-header d-flex flex-wrap align-items-center gap-2">
                  <span className="small text-muted">Tick a parent to grant everything under it. A partly ticked parent means only some of its permissions are granted.</span>
                  <span className="ms-auto d-flex gap-2">
                    <button type="button" className="btn btn-outline-secondary btn-sm" id="btnExpandAll" onClick={() => setCollapsed(new Set())}>Expand all</button>
                    <button type="button" className="btn btn-outline-secondary btn-sm" id="btnCollapseAll" onClick={collapseAll}>Collapse all</button>
                  </span>
                </div>
                {loadingRole ? (
                  <div className="card-body"><LoadingState text={`Loading permissions for ${selectedRole}...`} /></div>
                ) : visibleTree.length === 0 ? (
                  <div className="card-body text-muted small">No pages match "{search}".</div>
                ) : (
                  <ul className="perm-tree" id="permissionTree">
                    {visibleTree.map((root) => (
                      <TreeNode key={root.id} node={root} depth={0} selected={selected} collapsed={collapsed}
                                onToggle={(node, checked) => setSelected((s) => toggle(node, s, checked))} onExpandToggle={onExpandToggle} />
                    ))}
                  </ul>
                )}
              </div>
              <div className="card">
                <div className="card-body">
                  <div className="d-flex flex-wrap gap-2 align-items-center justify-content-between">
                    <span><i className="bi bi-check-circle-fill text-success me-1" /> {selectedCount} of {allPages.length} pages selected for <strong>{selectedRole}</strong></span>
                    <div>
                      <button type="button" className="btn btn-outline-primary btn-sm me-2" onClick={() => setSelected((s) => { const n = s.clone(); allPages.forEach((p) => n.add(p.pageKey)); return n; })}>Select All</button>
                      <button type="button" className="btn btn-outline-danger btn-sm" onClick={() => setSelected(new KeySet())}>Clear All</button>
                    </div>
                  </div>
                </div>
              </div>
            </>
          )}
        </>
      )}
    </>
  );
}
