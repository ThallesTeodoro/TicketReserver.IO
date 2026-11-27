namespace TR.HW.ResTimeout.UnitTests.Caching;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using TR.HW.ResTimeout.Caching;

public class SeatLockManagerTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ILogger<SeatLockManager>> _loggerMock = new();
    private readonly SeatLockManager _sut;

    public SeatLockManagerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _sut = new SeatLockManager(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ReleaseSeatLocksAsync_WhenLuaScriptReturnsOne_ShouldIncrementReleasedCount()
    {
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        _dbMock.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(k => k[0] == $"hold:event:{eventId}:seat:{seatId}"),
                It.Is<RedisValue[]>(v => v[0] == reservationId.ToString()),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        var count = await _sut.ReleaseSeatLocksAsync(eventId, [seatId], reservationId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task ReleaseSeatLocksAsync_WhenLuaScriptReturnsZero_ShouldReturnZeroReleased()
    {
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        _dbMock.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0));

        var count = await _sut.ReleaseSeatLocksAsync(eventId, [seatId], reservationId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task ReleaseSeatLocksAsync_WhenRedisThrows_ShouldCatchAndReturnZero()
    {
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        _dbMock.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new TimeoutException("Redis unavailable"));

        var count = await _sut.ReleaseSeatLocksAsync(eventId, [seatId], reservationId);

        count.Should().Be(0);
    }
}
