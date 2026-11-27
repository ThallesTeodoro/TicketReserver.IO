using FluentValidation;
using Microsoft.Extensions.Logging;
using TR.HW.Repl.Application.Common.Messaging;
using TR.HW.Repl.Application.Interfaces;
using TR.HW.Repl.Domain.Models;

namespace TR.HW.Repl.Application.Features.Replication;

public sealed class ReplicateEventChangeCommandHandler : ICommandHandler<ReplicateEventChangeCommand, ReplicationResult>
{
    private readonly IEventSearchRepository _repository;
    private readonly IValidator<ReplicateEventChangeCommand> _validator;
    private readonly ILogger<ReplicateEventChangeCommandHandler> _logger;

    public ReplicateEventChangeCommandHandler(
        IEventSearchRepository repository,
        IValidator<ReplicateEventChangeCommand> validator,
        ILogger<ReplicateEventChangeCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReplicationResult> HandleAsync(
        ReplicateEventChangeCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Validation failed for event CDC command: {Errors}", errors);
            return new ReplicationResult(false, errors);
        }

        try
        {
            if (command.Operation == CdcOperation.Delete)
            {
                _logger.LogInformation("Processing CDC DELETE for event {EventId}", command.EventId);
                await _repository.DeleteEventAsync(command.EventId, cancellationToken);
                return new ReplicationResult(true);
            }

            if (string.Equals(command.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Processing CDC status update CANCELLED for event {EventId}", command.EventId);
                await _repository.UpdateEventStatusAsync(command.EventId, "CANCELLED", DateTimeOffset.UtcNow, cancellationToken);
                return new ReplicationResult(true);
            }

            var existingDoc = await _repository.GetEventByIdAsync(command.EventId, cancellationToken);
            var docToUpsert = command.ToEventSearchDocument(existingDoc);

            await _repository.UpsertEventAsync(docToUpsert, cancellationToken);
            _logger.LogInformation("Successfully indexed event {EventId} in ElasticSearch", command.EventId);

            return new ReplicationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replicate event {EventId} to ElasticSearch", command.EventId);
            throw;
        }
    }
}

public sealed class ReplicateTicketTierChangeCommandHandler : ICommandHandler<ReplicateTicketTierChangeCommand, ReplicationResult>
{
    private readonly IEventSearchRepository _repository;
    private readonly IValidator<ReplicateTicketTierChangeCommand> _validator;
    private readonly ILogger<ReplicateTicketTierChangeCommandHandler> _logger;

    public ReplicateTicketTierChangeCommandHandler(
        IEventSearchRepository repository,
        IValidator<ReplicateTicketTierChangeCommand> validator,
        ILogger<ReplicateTicketTierChangeCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReplicationResult> HandleAsync(
        ReplicateTicketTierChangeCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Validation failed for ticket tier CDC command: {Errors}", errors);
            return new ReplicationResult(false, errors);
        }

        try
        {
            if (command.Operation == CdcOperation.Delete)
            {
                _logger.LogInformation("Processing CDC DELETE for tier {TierId} on event {EventId}", command.TierId, command.EventId);
                await _repository.RemoveTierPricingAsync(command.EventId, command.TierId, cancellationToken);
                return new ReplicationResult(true);
            }

            var tierSummary = command.ToTicketTierSummary();
            _logger.LogInformation("Processing CDC tier update for tier {TierId} ({TierName}) on event {EventId}", command.TierId, command.TierName, command.EventId);

            await _repository.UpdateTierPricingAsync(command.EventId, tierSummary, cancellationToken);
            return new ReplicationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replicate ticket tier {TierId} to ElasticSearch", command.TierId);
            throw;
        }
    }
}
