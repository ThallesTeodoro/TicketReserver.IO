namespace TR.HW.ReservationOutboxRelay.UnitTests.Persistence;

using System.Data.Common;
using Moq;
using TR.HW.ReservationOutboxRelay.Persistence;

public class OutboxRepositoryTests
{
    private readonly Mock<IDbConnectionFactory> _connectionFactoryMock = new();
    private readonly OutboxRepository _sut;

    public OutboxRepositoryTests()
    {
        _sut = new OutboxRepository(_connectionFactoryMock.Object);
    }

    [Fact]
    public void OutboxMessageRecord_ShouldBeConstructedCorrectly()
    {
        var id = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var record = new OutboxMessageRecord(
            id,
            correlationId,
            "reservation.checkout_req",
            "Reservation",
            aggregateId,
            "{\"amount\":100}",
            0,
            now,
            0,
            0,
            null,
            null,
            now);

        Assert.Equal(id, record.Id);
        Assert.Equal(correlationId, record.CorrelationId);
        Assert.Equal("reservation.checkout_req", record.EventType);
        Assert.Equal("Reservation", record.AggregateType);
        Assert.Equal(aggregateId, record.AggregateId);
        Assert.Equal(0, record.Status);
        Assert.Equal(0, record.RetryCount);
        Assert.Null(record.ErrorMessage);
    }

    [Fact]
    public void Repository_ShouldImplementIOutboxRepository()
    {
        Assert.IsAssignableFrom<IOutboxRepository>(_sut);
    }
}
