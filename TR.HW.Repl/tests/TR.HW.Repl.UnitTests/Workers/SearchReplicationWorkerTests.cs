using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TR.HW.Repl.Application.Common.Messaging;
using TR.HW.Repl.Application.Features.Replication;
using TR.HW.Repl.Worker.Messaging;
using TR.HW.Repl.Worker.Workers;
using Xunit;

namespace TR.HW.Repl.UnitTests.Workers;

public class SearchReplicationWorkerTests
{
    private readonly Mock<IKafkaConsumer> _consumerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ILogger<SearchReplicationWorker>> _loggerMock = new();
    private readonly Mock<ICommandHandler<ReplicateEventChangeCommand, ReplicationResult>> _eventHandlerMock = new();
    private readonly Mock<ICommandHandler<ReplicateTicketTierChangeCommand, ReplicationResult>> _tierHandlerMock = new();

    public SearchReplicationWorkerTests()
    {
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<ReplicateEventChangeCommand, ReplicationResult>)))
            .Returns(_eventHandlerMock.Object);
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<ReplicateTicketTierChangeCommand, ReplicationResult>)))
            .Returns(_tierHandlerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReceivesValidEventMessage_ShouldDispatchCommandAndCommit()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var json = "{\"payload\": {\"after\": {\"id\": \"" + eventId + "\", \"title\": \"Test Event\"}, \"op\": \"c\"}}";
        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "catalog.events.events",
            Message = new Message<string, string> { Key = eventId.ToString(), Value = json },
            Partition = new Partition(0),
            Offset = new Offset(10)
        };

        var calls = 0;
        _consumerMock.Setup(x => x.Consume(It.IsAny<TimeSpan>()))
            .Returns(() => calls++ == 0 ? consumeResult : null);

        _eventHandlerMock
            .Setup(x => x.HandleAsync(It.Is<ReplicateEventChangeCommand>(c => c.EventId == eventId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplicationResult(true));

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var worker = new SearchReplicationWorker(_consumerMock.Object, _scopeFactoryMock.Object, _loggerMock.Object);

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(x => x.Subscribe(), Times.Once);
        _consumerMock.Verify(x => x.Commit(consumeResult), Times.Once);
        _eventHandlerMock.Verify(x => x.HandleAsync(It.Is<ReplicateEventChangeCommand>(c => c.EventId == eventId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReceivesValidTicketTierMessage_ShouldDispatchCommandAndCommit()
    {
        // Arrange
        var tierId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var json = "{\"payload\": {\"after\": {\"id\": \"" + tierId + "\", \"event_id\": \"" + eventId + "\", \"name\": \"VIP\", \"price\": 100}, \"op\": \"c\"}}";
        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "catalog.events.ticket_tiers",
            Message = new Message<string, string> { Key = tierId.ToString(), Value = json },
            Partition = new Partition(0),
            Offset = new Offset(11)
        };

        var calls = 0;
        _consumerMock.Setup(x => x.Consume(It.IsAny<TimeSpan>()))
            .Returns(() => calls++ == 0 ? consumeResult : null);

        _tierHandlerMock
            .Setup(x => x.HandleAsync(It.Is<ReplicateTicketTierChangeCommand>(c => c.TierId == tierId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplicationResult(true));

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var worker = new SearchReplicationWorker(_consumerMock.Object, _scopeFactoryMock.Object, _loggerMock.Object);

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(x => x.Commit(consumeResult), Times.Once);
        _tierHandlerMock.Verify(x => x.HandleAsync(It.Is<ReplicateTicketTierChangeCommand>(c => c.TierId == tierId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReceivesPoisonPill_ShouldSendToDlqAndCommit()
    {
        // Arrange
        var malformedJson = "{ invalid-json }";
        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "catalog.events.events",
            Message = new Message<string, string> { Key = "bad-key", Value = malformedJson },
            Partition = new Partition(0),
            Offset = new Offset(12)
        };

        var calls = 0;
        _consumerMock.Setup(x => x.Consume(It.IsAny<TimeSpan>()))
            .Returns(() => calls++ == 0 ? consumeResult : null);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var worker = new SearchReplicationWorker(_consumerMock.Object, _scopeFactoryMock.Object, _loggerMock.Object);

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(x => x.SendToDlqAsync(
            consumeResult.Topic,
            "bad-key",
            malformedJson,
            It.IsAny<string>(),
            It.IsAny<Headers>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _consumerMock.Verify(x => x.Commit(consumeResult), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReceivesEmptyOrTombstoneMessage_ShouldCommitWithoutDispatching()
    {
        // Arrange
        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "catalog.events.events",
            Message = new Message<string, string> { Key = "tombstone-key", Value = "" },
            Partition = new Partition(0),
            Offset = new Offset(13)
        };

        var calls = 0;
        _consumerMock.Setup(x => x.Consume(It.IsAny<TimeSpan>()))
            .Returns(() => calls++ == 0 ? consumeResult : null);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var worker = new SearchReplicationWorker(_consumerMock.Object, _scopeFactoryMock.Object, _loggerMock.Object);

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(x => x.Commit(consumeResult), Times.Once);
        _eventHandlerMock.Verify(x => x.HandleAsync(It.IsAny<ReplicateEventChangeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
