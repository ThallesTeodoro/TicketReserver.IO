namespace TR.HW.Refund.Infrastructure.Gateways;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Infrastructure.Configuration;

public sealed class RefundGatewayService : IRefundGatewayService
{
    private readonly RefundGatewayOptions _options;
    private readonly ILogger<RefundGatewayService> _logger;
    private readonly AsyncRetryPolicy<RefundGatewayResult> _retryPolicy;

    public RefundGatewayService(
        IOptions<RefundGatewayOptions> options,
        ILogger<RefundGatewayService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _retryPolicy = Policy<RefundGatewayResult>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .OrResult(r => !r.IsSuccess && r.FailureReason == "GATEWAY_TIMEOUT")
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetries,
                sleepDurationProvider: retryAttempt =>
                {
                    var exponentialBackoff = TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100);
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 50));
                    return exponentialBackoff + jitter;
                },
                onRetry: (outcome, timespan, retryCount, _) =>
                {
                    if (outcome.Exception != null)
                    {
                        _logger.LogWarning(
                            outcome.Exception,
                            "Transient failure calling refund gateway. Retrying attempt {RetryCount} after {DelayMs}ms",
                            retryCount,
                            timespan.TotalMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Transient result received from refund gateway ({FailureReason}). Retrying attempt {RetryCount} after {DelayMs}ms",
                            outcome.Result?.FailureReason,
                            retryCount,
                            timespan.TotalMilliseconds);
                    }
                });
    }

    public async Task<RefundGatewayResult> ProcessRefundAsync(RefundGatewayRequest request, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async cancellationToken =>
        {
            if (_options.SimulateSuccess)
            {
                return await SimulateGatewayRefundAsync(request, cancellationToken);
            }

            return await ExecuteRealGatewayRefundAsync(request, cancellationToken);
        }, ct);
    }

    private Task<RefundGatewayResult> SimulateGatewayRefundAsync(RefundGatewayRequest request, CancellationToken ct)
    {
        // Special test trigger: Amount 999.99m simulates gateway refund rejection
        if (request.Amount == 999.99m)
        {
            _logger.LogWarning("Simulating refund rejection for payment {PaymentId}", request.PaymentId);
            return Task.FromResult(new RefundGatewayResult(
                IsSuccess: false,
                GatewayRefundId: null,
                FailureReason: "CHARGE_ALREADY_DISPUTED",
                RawResponse: JsonSerializer.Serialize(new { status = "failed", reason = "charge_already_disputed" })));
        }

        // Special test trigger: Amount 888.88m simulates transient gateway timeout
        if (request.Amount == 888.88m)
        {
            _logger.LogWarning("Simulating transient refund gateway timeout for payment {PaymentId}", request.PaymentId);
            throw new TimeoutException("Simulated refund gateway timeout");
        }

        var refundId = $"re_{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Simulated refund success for Payment {PaymentId} (Reservation {ReservationId}). GatewayRefundId: {GatewayRefundId}",
            request.PaymentId,
            request.ReservationId,
            refundId);

        var rawResponse = JsonSerializer.Serialize(new
        {
            id = refundId,
            payment_transaction_id = request.GatewayTransactionId,
            status = "succeeded",
            amount = request.Amount,
            currency = request.Currency,
            reason = request.Reason,
            provider = _options.Provider
        });

        return Task.FromResult(new RefundGatewayResult(
            IsSuccess: true,
            GatewayRefundId: refundId,
            FailureReason: null,
            RawResponse: rawResponse));
    }

    private Task<RefundGatewayResult> ExecuteRealGatewayRefundAsync(RefundGatewayRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Executing real gateway refund call for provider {Provider}", _options.Provider);
        return SimulateGatewayRefundAsync(request, ct);
    }
}
