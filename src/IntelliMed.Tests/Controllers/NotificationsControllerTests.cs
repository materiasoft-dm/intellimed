using System.Security.Claims;
using FluentAssertions;
using IntelliMed.Api.Controllers;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace IntelliMed.Tests.Controllers;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationRepository> _repositoryMock = new();
    private readonly NotificationsController _controller;

    private const string UserId = "user-1";

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController(_repositoryMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, UserId)
                    }, "TestAuth"))
                }
            }
        };
    }

    [Fact]
    public async Task GetMine_ReturnsCallersNotifications()
    {
        var notifications = new List<NotificationDto> { new() { Id = 1, Message = "Test" } };
        _repositoryMock.Setup(r => r.GetMyRecentAsync(UserId, 20)).ReturnsAsync(notifications);

        var result = await _controller.GetMine();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(notifications);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCountForCaller()
    {
        _repositoryMock.Setup(r => r.GetUnreadCountAsync(UserId)).ReturnsAsync(3);

        var result = await _controller.GetUnreadCount();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<UnreadCountDto>().Which.Count.Should().Be(3);
    }

    [Fact]
    public async Task MarkRead_Owned_ReturnsNoContent()
    {
        _repositoryMock.Setup(r => r.MarkReadAsync(5, UserId)).ReturnsAsync(true);

        var result = await _controller.MarkRead(5);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkRead_NotOwnedOrMissing_ReturnsNotFound()
    {
        _repositoryMock.Setup(r => r.MarkReadAsync(5, UserId)).ReturnsAsync(false);

        var result = await _controller.MarkRead(5);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task MarkAllRead_ReturnsCountUpdated()
    {
        _repositoryMock.Setup(r => r.MarkAllReadAsync(UserId)).ReturnsAsync(4);

        var result = await _controller.MarkAllRead();

        result.Should().BeOfType<OkObjectResult>();
    }
}
