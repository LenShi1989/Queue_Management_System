using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Queue.Api.Common;
using Queue.Application.Dtos;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Api.Controllers;

/// <summary>
/// 認證（§11 Auth、§20 JWT + RBAC）
/// </summary>
[ApiController]
[Route("api/auth")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Auth)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>登入並取得 JWT（測試帳號：admin / a12345678）</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "登入成功"));
    }

    /// <summary>取得目前登入者資訊與角色</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _auth.GetCurrentUserAsync(ct)));

    /// <summary>修改自己的密碼</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(request, ct);
        return Ok(ApiResponse.Ok(null, "密碼已更新"));
    }

    /// <summary>列出所有使用者（Admin）</summary>
    [HttpGet("users")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> GetUsers(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<UserDto>>.Ok(await _auth.GetUsersAsync(ct)));

    /// <summary>建立使用者（Admin）</summary>
    [HttpPost("users")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _auth.CreateUserAsync(request, ct), "建立成功"));

    /// <summary>更新使用者（Admin）</summary>
    [HttpPut("users/{id:long}")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(
        long id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _auth.UpdateUserAsync(id, request, ct), "更新成功"));

    /// <summary>重設使用者密碼（Admin）</summary>
    [HttpPost("users/{id:long}/reset-password")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(long id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(id, request.NewPassword, ct);
        return Ok(ApiResponse.Ok(null, "密碼已重設"));
    }

    /// <summary>刪除使用者（Admin）</summary>
    [HttpDelete("users/{id:long}")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(long id, CancellationToken ct)
    {
        await _auth.DeleteUserAsync(id, ct);
        return Ok(ApiResponse.Ok(null, "刪除成功"));
    }
}

public class ResetPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}
