namespace TR.API.Reservation.UnitTests.Infrastructure;

using FluentAssertions;
using Moq;
using StackExchange.Redis;
using TR.API.Reservation.Infrastructure.Caching;

public class RedisHoldServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();
    private readonly RedisHoldService _sut;

    public RedisHoldServiceTests()
    {
        _databaseMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
        _sut = new RedisHoldService(_redisMock.Object);
    }

    [Fact]
    public async Task AcquireSeatHoldAsync_WhenKeyNotExists_ShouldReturnTrue()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var resId = Guid.NewGuid();

        // Act
        var result = await _sut.AcquireSeatHoldAsync(eventId, seatId, resId, TimeSpan.FromMinutes(10));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseSeatHoldAsync_WhenLuaScriptReturnsOne_ShouldReturnTrue()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var resId = Guid.NewGuid();

        _databaseMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            CommandFlags.None))
        .ReturnsAsync(RedisResult.Create(1));

        // Act
        var result = await _sut.ReleaseSeatHoldAsync(eventId, seatId, resId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireTierQuotaHoldAsync_WhenQuotaSufficient_ShouldReturnTrue()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var resId = Guid.NewGuid();

        _databaseMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            CommandFlags.None))
        .ReturnsAsync(RedisResult.Create(1));

        // Act
        var result = await _sut.AcquireTierQuotaHoldAsync(eventId, tierId, 2, resId, TimeSpan.FromMinutes(10));

        // Assert
        result.Should().BeTrue();
    }
}
