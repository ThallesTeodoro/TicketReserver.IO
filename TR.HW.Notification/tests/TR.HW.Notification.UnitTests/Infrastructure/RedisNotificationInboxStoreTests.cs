using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using TR.HW.Notification.Infrastructure.Caching;
using TR.HW.Notification.Infrastructure.Configuration;

namespace TR.HW.Notification.UnitTests.Infrastructure;

public class RedisNotificationInboxStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<RedisNotificationInboxStore>> _loggerMock;
    private readonly IOptions<NotificationInfrastructureOptions> _options;
    private readonly RedisNotificationInboxStore _store;

    public RedisNotificationInboxStoreTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisNotificationInboxStore>>();
        _options = Microsoft.Extensions.Options.Options.Create(new NotificationInfrastructureOptions
        {
            InboxRetentionDays = 5
        });

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        _store = new RedisNotificationInboxStore(_redisMock.Object, _options, _loggerMock.Object);
    }

    [Fact]
    public void FormatKey_ShouldReturnFormattedKey()
    {
        // Act
        var key = RedisNotificationInboxStore.FormatKey("cg-notification-worker", "msg-12345");

        // Assert
        key.Should().Be("notif:inbox:cg-notification-worker:msg-12345");
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        var key = "notif:inbox:cg-notification-worker:msg-123";
        _dbMock.Setup(d => d.KeyExistsAsync((RedisKey)key, CommandFlags.None)).ReturnsAsync(true);

        // Act
        var result = await _store.HasBeenProcessedAsync("cg-notification-worker", "msg-123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WhenKeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var key = "notif:inbox:cg-notification-worker:msg-new";
        _dbMock.Setup(d => d.KeyExistsAsync((RedisKey)key, CommandFlags.None)).ReturnsAsync(false);

        // Act
        var result = await _store.HasBeenProcessedAsync("cg-notification-worker", "msg-new");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldCallStringSetWithExpectedTtl()
    {
        // Arrange
        var expectedKey = "notif:inbox:cg-notification-worker:msg-456";
        _dbMock
            .Setup(d => d.StringSetAsync(
                (RedisKey)expectedKey,
                It.IsAny<RedisValue>(),
                TimeSpan.FromDays(5),
                When.Always))
            .ReturnsAsync(true);

        // Act
        await _store.MarkAsProcessedAsync("cg-notification-worker", "msg-456");

        // Assert
        _dbMock.Verify(d => d.StringSetAsync(
            (RedisKey)expectedKey,
            It.IsAny<RedisValue>(),
            TimeSpan.FromDays(5),
            When.Always), Times.Once);
    }

    [Theory]
    [InlineData("", "msg-1")]
    [InlineData("cg", "")]
    public async Task Methods_WithEmptyArguments_ShouldThrowArgumentException(string cg, string msgId)
    {
        // Act
        var actExists = () => _store.HasBeenProcessedAsync(cg, msgId);
        var actMark = () => _store.MarkAsProcessedAsync(cg, msgId);

        // Assert
        await actExists.Should().ThrowAsync<ArgumentException>();
        await actMark.Should().ThrowAsync<ArgumentException>();
    }
}
