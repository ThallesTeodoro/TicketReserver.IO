namespace TR.Common.Diagnostics.UnitTests;

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using TicketReserver.Diagnostics;
using Xunit;

public class KafkaTraceContextPropagatorTests
{
    static KafkaTraceContextPropagatorTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    }

    [Fact]
    public void Inject_ShouldAddTraceParentHeader_WhenActivityIsActive()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("Test.Source");
        using var activity = source.StartActivity("ProduceMessage");
        activity.Should().NotBeNull();

        var headers = new Headers();

        // Act
        KafkaTraceContextPropagator.Inject(headers, activity);

        // Assert
        headers.TryGetLastBytes(KafkaTraceContextPropagator.TraceParentHeader, out var bytes).Should().BeTrue();
        var traceparent = Encoding.UTF8.GetString(bytes!);
        traceparent.Should().Be(activity!.Id);
    }

    [Fact]
    public void ExtractAndStartActivity_ShouldCreateChildSpan_WhenValidTraceParentHeaderExists()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var originalTraceId = ActivityTraceId.CreateRandom();
        var originalSpanId = ActivitySpanId.CreateRandom();
        var expectedTraceParent = $"00-{originalTraceId}-{originalSpanId}-01";

        var headers = new Headers
        {
            { KafkaTraceContextPropagator.TraceParentHeader, Encoding.UTF8.GetBytes(expectedTraceParent) }
        };

        // Act
        using var consumerActivity = KafkaTraceContextPropagator.ExtractAndStartActivity(headers, "ConsumeMessage");

        // Assert
        consumerActivity.Should().NotBeNull();
        consumerActivity!.TraceId.ToString().Should().Be(originalTraceId.ToString());
        consumerActivity.ParentSpanId.ToString().Should().Be(originalSpanId.ToString());
        consumerActivity.SpanId.ToString().Should().NotBe(originalSpanId.ToString());
    }

    [Fact]
    public void ExtractAndStartActivity_ShouldStartNewTrace_WhenNoHeadersProvided()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = KafkaTraceContextPropagator.ExtractAndStartActivity(null, "ConsumeMessage");

        // Assert
        activity.Should().NotBeNull();
        activity!.ParentSpanId.Should().Be(default(ActivitySpanId));
    }
}
