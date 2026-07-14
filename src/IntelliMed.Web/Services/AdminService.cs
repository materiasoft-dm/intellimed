using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

/// <summary>
/// Service for admin-level API calls: user management, role management.
/// </summary>
public interface IAdminService
{
    Task<List<UserDto>?> GetUsersAsync();
    Task<UserDto?> GetUserAsync(string id);
    Task<UserManagementResponse?> CreateUserAsync(CreateUserRequest request);
    Task<UserManagementResponse?> UpdateUserAsync(string id, UpdateUserRequest request);
    Task<UserManagementResponse?> DeleteUserAsync(string id);
    Task<UserManagementResponse?> ResetPasswordAsync(string id, ResetPasswordRequest request);
    Task<List<RoleDto>?> GetRolesAsync();
    Task<UserManagementResponse?> AssignRolesAsync(string id, AssignRolesRequest request);

    // Dynamic page permissions
    Task<List<PageDefinitionDto>?> GetPageDefinitionsAsync();
    Task<RolePermissionsDto?> GetRolePermissionsAsync(string roleName);
    Task<UserManagementResponse?> SaveRolePermissionsAsync(string roleName, SaveRolePermissionsRequest request);
    Task<List<string>?> GetMyAccessiblePagesAsync();
}

public class AdminService : IAdminService
{
    private readonly HttpClient _httpClient;

    public AdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<UserDto>?> GetUsersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<UserDto>>("api/admin/users");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetUsers error: {ex.Message}");
            return null;
        }
    }

    public async Task<UserDto?> GetUserAsync(string id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserDto>($"api/admin/users/{id}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetUser error: {ex.Message}");
            return null;
        }
    }

    public async Task<UserManagementResponse?> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/admin/users", request);
            return await response.Content.ReadFromJsonAsync<UserManagementResponse>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CreateUser error: {ex.Message}");
            return new UserManagementResponse { Success = false, Message = "Connection error." };
        }
    }

    public async Task<UserManagementResponse?> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/admin/users/{id}", request);
            return await response.Content.ReadFromJsonAsync<UserManagementResponse>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UpdateUser error: {ex.Message}");
            return new UserManagementResponse { Success = false, Message = "Connection error." };
        }
    }

    public async Task<UserManagementResponse?> DeleteUserAsync(string id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/admin/users/{id}");
            return await response.Content.ReadFromJsonAsync<UserManagementResponse>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"DeleteUser error: {ex.Message}");
            return new UserManagementResponse { Success = false, Message = "Connection error." };
        }
    }

    public async Task<UserManagementResponse?> ResetPasswordAsync(string id, ResetPasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/admin/users/{id}/reset-password", request);
            return await response.Content.ReadFromJsonAsync<UserManagementResponse>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ResetPassword error: {ex.Message}");
            return new UserManagementResponse { Success = false, Message = "Connection error." };
        }
    }

    public async Task<List<RoleDto>?> GetRolesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<RoleDto>>("api/admin/roles");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetRoles error: {ex.Message}");
            return null;
        }
    }

    public async Task<UserManagementResponse?> AssignRolesAsync(string id, AssignRolesRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/admin/users/{id}/roles", request);
            return await response.Content.ReadFromJsonAsync<UserManagementResponse>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AssignRoles error: {ex.Message}");
            return new UserManagementResponse { Success = false, Message = "Connection error." };
        }
    }

    // =========================================================================
    // Dynamic page permissions
    // =========================================================================

    public async Task<List<PageDefinitionDto>?> GetPageDefinitionsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<PageDefinitionDto>>("api/admin/role-permissions/page-definitions");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetPageDefinitions error: {ex.Message}");
            return null;
        }
    }

    public async Task<RolePermissionsDto?> GetRolePermissionsAsync(string roleName)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RolePermissionsDto>($"api/admin/role-permissions/{roleName}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetRolePermissions error: {ex.Message}");
            return null;
        }
    }

    public async Task<UserManagementResponse?> SaveRolePermissionsAsync(string roleName, SaveRolePermissionsRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/admin/role-permissions/{roleName}", request);
            return await response.Content.ReadFromJsonAsync<UserManagementResponse>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SaveRolePermissions error: {ex.Message}");
            return new UserManagementResponse { Success = false, Message = "Connection error." };
        }
    }

    public async Task<List<string>?> GetMyAccessiblePagesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>("api/admin/role-permissions/my-pages");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetMyAccessiblePages error: {ex.Message}");
            return null;
        }
    }
}