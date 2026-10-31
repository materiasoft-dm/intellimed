import { getOrNull, getOr, ok, sendResult, sendWithError } from './http';
import { storage, StorageKeys } from './storage';
import type {
  AssignRolesRequest, ClinicDto, ClinicUserDto, CreateClinicDto, CreateUserRequest, MyClinicDto, PageDefinitionDto,
  ResetPasswordRequest, RoleDto, RolePermissionsDto, SaveRolePermissionsRequest, SetClinicUsersRequest, UpdateClinicDto,
  UpdateUserRequest, UserDto, UserManagementResponse,
} from './types.gen';

const connectionError = { success: false, message: 'Connection error.' } as UserManagementResponse;

export const AdminApi = {
  getUsers: () => getOrNull<UserDto[]>('api/admin/users'),
  getUser: (id: string) => getOrNull<UserDto>(`api/admin/users/${id}`),
  createUser: (request: CreateUserRequest) => sendResult('POST', 'api/admin/users', request, connectionError),
  updateUser: (id: string, request: UpdateUserRequest) => sendResult('PUT', `api/admin/users/${id}`, request, connectionError),
  deleteUser: (id: string) => sendResult('DELETE', `api/admin/users/${id}`, undefined, connectionError),
  resetPassword: (id: string, request: ResetPasswordRequest) =>
    sendResult('POST', `api/admin/users/${id}/reset-password`, request, connectionError),
  sendInvite: (id: string) => sendResult('POST', `api/admin/users/${id}/send-invite`, undefined, connectionError),
  getRoles: () => getOrNull<RoleDto[]>('api/admin/roles'),
  assignRoles: (id: string, request: AssignRolesRequest) => sendResult('PUT', `api/admin/users/${id}/roles`, request, connectionError),
  getPageDefinitions: () => getOrNull<PageDefinitionDto[]>('api/admin/role-permissions/page-definitions'),
  getRolePermissions: (roleName: string) => getOrNull<RolePermissionsDto>(`api/admin/role-permissions/${encodeURIComponent(roleName)}`),
  saveRolePermissions: (roleName: string, request: SaveRolePermissionsRequest) =>
    sendResult('PUT', `api/admin/role-permissions/${encodeURIComponent(roleName)}`, request, connectionError),
  getMyAccessiblePages: () => getOrNull<string[]>('api/admin/role-permissions/my-pages'),
};

export const ClinicApi = {
  getMyClinics: () => getOrNull<MyClinicDto[]>('api/clinics/my-clinics'),
  getLookup: () => getOr<MyClinicDto[]>('api/clinics/lookup', []),
  getAll: () => getOrNull<ClinicDto[]>('api/clinics'),
  getById: (id: number) => getOrNull<ClinicDto>(`api/clinics/${id}`),
  create: (dto: CreateClinicDto) => ok('POST', 'api/clinics', dto),
  createWithError: (dto: CreateClinicDto) => sendWithError('POST', 'api/clinics', dto),
  update: (id: number, dto: UpdateClinicDto) => ok('PUT', `api/clinics/${id}`, dto),
  getUsers: (id: number) => getOrNull<ClinicUserDto[]>(`api/clinics/${id}/users`),
  setUsers: (id: number, request: SetClinicUsersRequest) => ok('PUT', `api/clinics/${id}/users`, request),
};

/** Which clinic the user is working in; every request carries it as X-Clinic-Id (see http.ts). */
export const ClinicContext = {
  get(): number | null {
    const value = storage.get(StorageKeys.clinicId);
    const id = value ? Number.parseInt(value, 10) : NaN;
    return Number.isNaN(id) ? null : id;
  },
  set(clinicId: number) {
    storage.set(StorageKeys.clinicId, String(clinicId));
  },
  clear() {
    storage.set(StorageKeys.clinicId, null);
  },
};
