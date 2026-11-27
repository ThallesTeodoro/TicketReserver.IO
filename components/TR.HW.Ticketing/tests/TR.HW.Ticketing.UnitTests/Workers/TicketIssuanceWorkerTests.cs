namespace TR.HW.Ticketing.UnitTests.Workers;

using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Ticketing.Application.Common.Messaging;
using TR.HW.Ticketing.Application.Features.IssueTickets;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Worker.Configuration;
using TR.HW.Ticketing.Worker.Messaging;
using TR.HW.Ticketing.Worker.Workers;

public sealed class TicketIssuanceWorkerTests
{
    private readonly Mock<IKafkaConsumer> _mockKafkaConsumer;
    private readonly Mock<ITicketingRepository> _mockRepository;
    private readonly Mock<ICommandHandler<IssueTicketsCommand, IssueTicketsResult>> _mockHandler;
    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public TicketIssuanceWorkerTests()
    {
        _mockKafkaConsumer = new Mock<IKafkaConsumer>();
        _mockRepository = new Mock<ITicketingRepository>();
        _mockHandler = new Mock<ICommandHandler<IssueTicketsCommand, IssueTicketsResult>>();

        _options = new KafkaConsumerOptions
        {
            Topic = "payment.v1.approved",
            GroupId = "cg-ticketing-worker",
            DeadLetterQueueTopic = "payment.v1.approved.DLQ"
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => _mockRepository.Object);
        serviceCollection.AddScoped(_ => _mockHandler.Object);
        var provider = serviceCollection.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldInvokeCommandHandler_WhenMessageIsNewAndValidWithItems()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var catalogEventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var tierId = Guid.NewGuid();

        var payloadObj = new
        {
            eventId = Guid.NewGuid().ToString(),
            reservationId,
            paymentId,
            customerId,
            catalogEventId,
            amount = 180.00m,
            items = new[]
            {
                new
                {
                    ticketTierId = tierId,
                    seatId,
                    seatLabel = "Setor A - Cadeira 15",
                    unitPrice = 180.00m
                }
            }
        };

        var rawJson = JsonSerializer.Serialize(payloadObj);
        var message = new Message<string, string>
        {
            Key = reservationId.ToString(),
            Value = rawJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(10)
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync(It.IsAny<string>(), _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<IssueTicketsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueTicketsResult(
                true,
                "ISSUED",
                new[] { new IssuedTicketSummary(Guid.NewGuid(), "TKT-2026-123456", "qr_123", seatId, "Setor A - Cadeira 15", 180.00m) }));

        var worker = new TicketIssuanceWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<TicketIssuanceWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(
            It.Is<IssueTicketsCommand>(cmd =>
                cmd.ReservationId == reservationId &&
                cmd.PaymentId == paymentId &&
                cmd.Items.Count == 1 &&
                cmd.Items[0].SeatId == seatId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldSkipHandler_WhenMessageAlreadyProcessedInInbox()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var rawJson = JsonSerializer.Serialize(new
        {
            reservationId,
            paymentId = Guid.NewGuid(),
            customerId = Guid.NewGuid(),
            amount = 150.00m
        });

        var message = new Message<string, string>
        {
            Key = reservationId.ToString(),
            Value = rawJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(20)
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync(It.IsAny<string>(), _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var worker = new TicketIssuanceWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<TicketIssuanceWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<IssueTicketsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldRouteToDlq_WhenJsonIsPoisoned()
    {
        // Arrange
        const string poisonedJson = "invalid-json{{{";
        var message = new Message<string, string>
        {
            Key = "bad-key",
            Value = poisonedJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(30)
        };

        var worker = new TicketIssuanceWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<TicketIssuanceWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockKafkaConsumer.Verify(k => k.SendToDlqAsync(
            "bad-key",
            poisonedJson,
            It.Is<string>(reason => reason.Contains("deserialization")),
            It.IsAny<Headers>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<IssueTicketsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
