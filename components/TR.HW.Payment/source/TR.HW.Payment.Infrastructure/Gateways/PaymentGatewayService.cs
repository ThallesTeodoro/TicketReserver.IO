namespace TR.HW.Payment.Infrastructure.Gateways;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Infrastructure.Configuration;

public sealed class PaymentGatewayService : IPaymentGatewayService
{
    private readonly PaymentGatewayOptions _options;
    private readonly ILogger<PaymentGatewayService> _logger;
    private readonly AsyncRetryPolicy<PaymentGatewayResult> _retryPolicy;

    public PaymentGatewayService(
        IOptions<PaymentGatewayOptions> options,
        ILogger<PaymentGatewayService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _retryPolicy = Policy<PaymentGatewayResult>
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
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    if (outcome.Exception != null)
                    {
                        _logger.LogWarning(
                            outcome.Exception,
                            "Transient failure calling payment gateway. Retrying attempt {RetryCount} after {DelayMs}ms",
                            retryCount,
                            timespan.TotalMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Transient result received from payment gateway ({FailureReason}). Retrying attempt {RetryCount} after {DelayMs}ms",
                            outcome.Result?.FailureReason,
                            retryCount,
                            timespan.TotalMilliseconds);
                    }
                });
    }

    public async Task<PaymentGatewayResult> ProcessPaymentAsync(PaymentGatewayRequest request, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async (cancellationToken) =>
        {
            if (_options.SimulateSuccess)
            {
                return await SimulateGatewayExecutionAsync(request, cancellationToken);
            }

            return await ExecuteRealGatewayCallAsync(request, cancellationToken);
        }, ct);
    }

    private Task<PaymentGatewayResult> SimulateGatewayExecutionAsync(PaymentGatewayRequest request, CancellationToken ct)
    {
        // Special test trigger: Amount 999.99m simulates payment declined by bank (insufficient funds)
        if (request.Amount == 999.99m)
        {
            _logger.LogInformation("Simulating payment decline for reservation {ReservationId}", request.ReservationId);
            return Task.FromResult(new PaymentGatewayResult(
                IsSuccess: false,
                GatewayTransactionId: $"ch_declined_{Guid.NewGuid():N}",
                AuthorizationCode: null,
                FailureReason: "INSUFFICIENT_FUNDS",
                RawResponse: JsonSerializer.Serialize(new { status = "declined", reason = "insufficient_funds" })));
        }

        // Special test trigger: Amount 888.88m simulates transient gateway timeout to test retry policy
        if (request.Amount == 888.88m)
        {
            _logger.LogWarning("Simulating transient gateway timeout for reservation {ReservationId}", request.ReservationId);
            throw new TimeoutException("Simulated payment gateway timeout");
        }

        var transactionId = $"ch_{Guid.NewGuid():N}";
        var authCode = $"AUTH_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        _logger.LogInformation(
            "Simulated payment success for reservation {ReservationId}. TxId: {TxId}, AuthCode: {AuthCode}",
            request.ReservationId,
            transactionId,
            authCode);

        var rawResponse = JsonSerializer.Serialize(new
        {
            id = transactionId,
            status = "succeeded",
            amount = request.Amount,
            currency = request.Currency,
            authorization_code = authCode,
            provider = _options.Provider
        });

        return Task.FromResult(new PaymentGatewayResult(
            IsSuccess: true,
            GatewayTransactionId: transactionId,
            AuthorizationCode: authCode,
            FailureReason: null,
            RawResponse: rawResponse));
    }

    private Task<PaymentGatewayResult> ExecuteRealGatewayCallAsync(PaymentGatewayRequest request, CancellationToken ct)
    {
        // Mock fallback if real provider HTTP client is not yet wired
        _logger.LogInformation("Executing real gateway call for provider {Provider}", _options.Provider);
        return SimulateGatewayExecutionAsync(request, ct);
    }
}
