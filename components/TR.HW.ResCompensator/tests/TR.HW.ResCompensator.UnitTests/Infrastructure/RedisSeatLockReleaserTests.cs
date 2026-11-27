namespace TR.HW.ResCompensator.UnitTests.Infrastructure;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using TR.HW.ResCompensator.Infrastructure.Caching;

public sealed class RedisSeatLockReleaserTests
{
    private readonly Mock<IConnectionMultiplexer> _mockMultiplexer;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly RedisSeatLockReleaser _releaser;

    public RedisSeatLockReleaserTests()
    {
        _mockMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockMultiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _releaser = new RedisSeatLockReleaser(
            _mockMultiplexer.Object,
            NullLogger<RedisSeatLockReleaser>.Instance);
    }

    [Fact]
    public async Task ReleaseHeldSeatsAsync_ShouldEvaluateLuaScriptAndReturnReleasedCount_WhenLocksMatchReservation()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var seatId1 = Guid.NewGuid();
        var seatId2 = Guid.NewGuid();

        _mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        var count = await _releaser.ReleaseHeldSeatsAsync(
            eventId,
            reservationId,
            new[] { seatId1, seatId2 });

        // Assert
        count.Should().Be(2);

        var expectedKey1 = $"hold:event:{eventId}:seat:{seatId1}";
        var expectedKey2 = $"hold:event:{eventId}:seat:{seatId2}";

        _mockDatabase.Verify(db => db.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("redis.call(\"get\", KEYS[1]) == ARGV[1]")),
            It.Is<RedisKey[]?>(k => k != null && k[0] == expectedKey1),
            It.Is<RedisValue[]?>(v => v != null && v[0] == reservationId.ToString()),
            It.IsAny<CommandFlags>()),
            Times.Once);

        _mockDatabase.Verify(db => db.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("redis.call(\"get\", KEYS[1]) == ARGV[1]")),
            It.Is<RedisKey[]?>(k => k != null && k[0] == expectedKey2),
            It.Is<RedisValue[]?>(v => v != null && v[0] == reservationId.ToString()),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ReleaseHeldSeatsAsync_ShouldReturnZero_WhenLockOwnedByAnotherReservation()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        _mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0));

        // Act
        var count = await _releaser.ReleaseHeldSeatsAsync(
            eventId,
            reservationId,
            new[] { seatId });

        // Assert
        count.Should().Be(0);
    }
}
