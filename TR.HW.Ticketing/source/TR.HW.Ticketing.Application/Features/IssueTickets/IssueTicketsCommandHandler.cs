namespace TR.HW.Ticketing.Application.Features.IssueTickets;

using FluentValidation;
using Microsoft.Extensions.Logging;
using TR.HW.Ticketing.Application.Common.Messaging;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Domain.Entities;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Domain.Exceptions;
using TR.HW.Ticketing.Domain.Services;

public sealed class IssueTicketsCommandHandler(
    ITicketingRepository ticketingRepository,
    IQrCodeGenerator qrCodeGenerator,
    IKafkaTicketingProducer kafkaTicketingProducer,
    IValidator<IssueTicketsCommand> validator,
    ILogger<IssueTicketsCommandHandler> logger) : ICommandHandler<IssueTicketsCommand, IssueTicketsResult>
{
    private const string ConsumerGroup = "cg-ticketing-worker";

    public async Task<IssueTicketsResult> HandleAsync(IssueTicketsCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            logger.LogWarning("Validation failed for IssueTicketsCommand: {Errors}", errors);
            throw new ValidationException(validationResult.Errors);
        }

        var messageId = command.MessageId ?? command.PaymentId.ToString();

        // 1. Idempotency check by ReservationId
        if (await ticketingRepository.HasExistingTicketsForReservationAsync(command.ReservationId, ct))
        {
            logger.LogInformation("Reservation {ReservationId} already has issued tickets. Skipping duplicate request.", command.ReservationId);
            await ticketingRepository.RecordProcessedMessageAsync(messageId, ConsumerGroup, "payment.v1.approved", ct);
            return new IssueTicketsResult(true, "ALREADY_ISSUED", Array.Empty<IssuedTicketSummary>());
        }

        // 2. Build Ticket aggregates and SeatAllocation entities
        var tickets = new List<Ticket>(command.Items.Count);
        var allocations = new List<SeatAllocation>(command.Items.Count);
        var issuedEvents = new List<TicketIssuedDomainEvent>(command.Items.Count);

        foreach (var item in command.Items)
        {
            var ticket = Ticket.Issue(
                reservationId: command.ReservationId,
                paymentId: command.PaymentId,
                customerId: command.CustomerId,
                eventId: command.EventId,
                ticketTierId: item.TicketTierId,
                pricePaid: item.UnitPrice,
                qrCodeGenerator: qrCodeGenerator,
                seatId: item.SeatId,
                seatLabel: item.SeatLabel);

            var allocation = ticket.CreateSeatAllocation();

            tickets.Add(ticket);
            allocations.Add(allocation);
            issuedEvents.AddRange(ticket.DomainEvents.OfType<TicketIssuedDomainEvent>());
        }

        // 3. Persist tickets, seat allocations, outbox, and inbox atomically
        try
        {
            await ticketingRepository.SaveTicketsAndAllocationsTransactionallyAsync(
                tickets,
                allocations,
                messageId,
                ConsumerGroup,
                ct);
        }
        catch (SeatAlreadyAllocatedException ex)
        {
            logger.LogWarning(
                ex,
                "Concurrency seat collision detected for Reservation {ReservationId}, Event {EventId}, Seat {SeatId}. Initiating compensation saga (ticketing.v1.failed).",
                command.ReservationId,
                ex.EventId,
                ex.SeatId);

            var failedEvent = new TicketingFailedDomainEvent(
                ReservationId: command.ReservationId,
                PaymentId: command.PaymentId,
                CustomerId: command.CustomerId,
                EventId: command.EventId,
                FailureReason: "SEAT_ALREADY_ALLOCATED",
                OccurredOn: DateTime.UtcNow);

            await ticketingRepository.RecordTicketingFailedTransactionallyAsync(
                failedEvent,
                messageId,
                ConsumerGroup,
                ct);

            await kafkaTicketingProducer.PublishTicketingFailedAsync(failedEvent, command.Traceparent, ct);

            return new IssueTicketsResult(
                IsSuccess: false,
                Status: "FAILED_SEAT_COLLISION",
                IssuedTickets: Array.Empty<IssuedTicketSummary>(),
                FailureReason: "SEAT_ALREADY_ALLOCATED");
        }

        // 4. Publish ticketing.v1.issued event to Kafka
        await kafkaTicketingProducer.PublishTicketsIssuedAsync(
            command.ReservationId,
            command.PaymentId,
            command.CustomerId,
            command.EventId,
            issuedEvents,
            command.Traceparent,
            ct);

        foreach (var t in tickets)
        {
            t.ClearDomainEvents();
        }

        return tickets.ToIssueTicketsResult();
    }
}
