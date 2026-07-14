using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using IntelliMed.Api.Controllers;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using IdentityCore = IntelliMed.Core.Entities;

namespace IntelliMed.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<UserManager<IdentityCore.ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<IdentityCore.ApplicationUser>> _signInManagerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<IdentityCore.ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<IdentityCore.ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Setup SignInManager mock
        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<IdentityCore.ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<IdentityCore.ApplicationUser>>(
            _userManagerMock.Object, contextAccessorMock.Object, claimsFactoryMock.Object, null!, null!, null!, null!);

        // Setup Configuration mock
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["Jwt:Key"]).Returns("IntelliMed_SuperSecretKey_AtLeast32Characters!");
        _configurationMock.Setup(c => c["Jwt:Issuer"]).Returns("IntelliMed");
        _configurationMock.Setup(c => c["Jwt:Audience"]).Returns("IntelliMed.Client");
        _configurationMock.Setup(c => c["Jwt:ExpirationHours"]).Returns("8");

        // Setup Logger mock
        _loggerMock = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        // Setup ControllerContext for proper ActionResult resolution
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var user = new IdentityCore.ApplicationUser
        {
            Id = "user-id-1",
            Email = "test@example.com",
            UserName = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<IdentityCore.ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Patient" });

        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeAssignableTo<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LoginResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Token.Should().NotBeNullOrEmpty();
        response.Email.Should().Be("test@example.com");
        response.FullName.Should().Be("John Doe");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "nonexistent@example.com", Password = "Password123!" };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityCore.ApplicationUser?)null);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<LoginResponse>().Subject;
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Invalid email or password");
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "inactive@example.com", Password = "Password123!" };
        var user = new IdentityCore.ApplicationUser
        {
            Id = "user-id-1",
            Email = "inactive@example.com",
            UserName = "inactive@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            IsActive = false
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<LoginResponse>().Subject;
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Account is deactivated. Please contact administrator.");
    }

    [Fact]
    public async Task Login_WithLockedOutUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "locked@example.com", Password = "Password123!" };
        var user = new IdentityCore.ApplicationUser
        {
            Id = "user-id-1",
            Email = "locked@example.com",
            UserName = "locked@example.com",
            FirstName = "Locked",
            LastName = "User",
            IsActive = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Result.Should().BeAssignableTo<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<LoginResponse>().Subject;
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "WrongPassword!" };
        var user = new IdentityCore.ApplicationUser
        {
            Id = "user-id-1",
            Email = "test@example.com",
            UserName = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Result.Should().BeAssignableTo<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<LoginResponse>().Subject;
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Invalid email or password");
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        // Arrange
        _signInManagerMock.Setup(x => x.SignOutAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LogoutResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentUser_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        // Setup HttpContext with the claims principal
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = claimsPrincipal
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _userManagerMock.Setup(x => x.GetUserAsync(claimsPrincipal))
            .ReturnsAsync((IdentityCore.ApplicationUser?)null);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<CurrentUserResponse>().Subject;
        response.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsUserInfo()
    {
        // Arrange
        var user = new IdentityCore.ApplicationUser
        {
            Id = "user-id-1",
            Email = "test@example.com",
            UserName = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email)
        }, "TestAuth"));

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = claimsPrincipal
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _userManagerMock.Setup(x => x.GetUserAsync(claimsPrincipal))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Patient", "Admin" });

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CurrentUserResponse>().Subject;
        response.IsAuthenticated.Should().BeTrue();
        response.Email.Should().Be("test@example.com");
        response.FullName.Should().Be("John Doe");
        response.Roles.Should().Contain("Patient");
        response.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Login_GeneratesValidJwtToken()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var user = new IdentityCore.ApplicationUser
        {
            Id = "user-id-1",
            Email = "test@example.com",
            UserName = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<IdentityCore.ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Patient" });

        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeAssignableTo<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LoginResponse>().Subject;
        
        response.Token.Should().NotBeNullOrEmpty();
        
        // Verify the token is valid JWT
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(response.Token);
        
        jwtToken.Issuer.Should().Be("IntelliMed");
        jwtToken.Audiences.Should().Contain("IntelliMed.Client");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "John Doe");
    }
}