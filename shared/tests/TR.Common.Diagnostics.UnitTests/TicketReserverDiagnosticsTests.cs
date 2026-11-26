namespace TR.Common.Diagnostics.UnitTests;

using FluentAssertions;
using TicketReserver.Diagnostics;
using Xunit;

public class TicketReserverDiagnosticsTests
{
    [Fact]
    public void TicketReserverDiagnostics_ShouldHaveConfiguredInstruments()
    {
        TicketReserverDiagnostics.Meter.Should().NotBeNull();
        TicketReserverDiagnostics.MeterName.Should().Be("TicketReserver");
        TicketReserverDiagnostics.KafkaActivitySourceName.Should().Be("TicketReserver.Kafka");
        TicketReserverDiagnostics.HttpActivitySourceName.Should().Be("TicketReserver.Http");

        TicketReserverDiagnostics.RedisLockAcquireDuration.Should().NotBeNull();
        TicketReserverDiagnostics.ActiveReservations.Should().NotBeNull();
        TicketReserverDiagnostics.SagaEventsTotal.Should().NotBeNull();
        TicketReserverDiagnostics.SeatCollisionRefundsTotal.Should().NotBeNull();
        TicketReserverDiagnostics.OutboxPendingMessages.Should().NotBeNull();
    }

    [Fact]
    public void RecordMethods_ShouldExecuteWithoutExceptions()
    {
        var act1 = () => TicketReserverDiagnostics.RecordRedisLockAcquireDuration(0.015, true);
        var act2 = () => TicketReserverDiagnostics.UpdateActiveReservations(42);
        var act3 = () => TicketReserverDiagnostics.RecordSagaEvent("payment.v1.approved");
        var act4 = () => TicketReserverDiagnostics.RecordSeatCollisionRefund();
        var act5 = () => TicketReserverDiagnostics.UpdateOutboxPendingMessages(10);

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
        act5.Should().NotThrow();
    }
}
