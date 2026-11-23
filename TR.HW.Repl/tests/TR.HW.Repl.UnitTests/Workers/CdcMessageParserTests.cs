using FluentAssertions;
using TR.HW.Repl.Domain.Models;
using TR.HW.Repl.Worker.Messaging;
using Xunit;

namespace TR.HW.Repl.UnitTests.Workers;

public class CdcMessageParserTests
{
    [Fact]
    public void ParseEventCommand_WithDebeziumEnvelope_ShouldExtractEventFields()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var json = $$"""
        {
            "schema": { "type": "struct" },
            "payload": {
                "before": null,
                "after": {
                    "id": "{{eventId}}",
                    "title": "Rock in Rio 2026",
                    "slug": "rock-in-rio-2026",
                    "description": "Festival de música",
                    "category": "MUSIC",
                    "status": "PUBLISHED",
                    "age_restriction": 16,
                    "banner_url": "https://cdn.example.com/rir.jpg",
                    "sales_start_at": "2026-06-01T10:00:00Z",
                    "sales_end_at": "2026-09-01T22:00:00Z",
                    "event_start_at": "2026-09-15T14:00:00Z",
                    "event_end_at": "2026-09-20T23:59:59Z",
                    "venue_id": "{{venueId}}",
                    "venue_name": "Cidade do Rock",
                    "venue_city": "Rio de Janeiro",
                    "venue_state": "RJ",
                    "venue_country": "BR",
                    "venue_latitude": -22.98,
                    "venue_longitude": -43.41,
                    "tags": ["rock", "festival", "musica"]
                },
                "op": "c",
                "ts_ms": 1718000000000
            }
        }
        """;

        // Act
        var command = CdcMessageParser.ParseEventCommand(json);

        // Assert
        command.Should().NotBeNull();
        command.EventId.Should().Be(eventId);
        command.Operation.Should().Be(CdcOperation.Create);
        command.Title.Should().Be("Rock in Rio 2026");
        command.Slug.Should().Be("rock-in-rio-2026");
        command.Category.Should().Be("MUSIC");
        command.Status.Should().Be("PUBLISHED");
        command.AgeRestriction.Should().Be(16);
        command.VenueId.Should().Be(venueId);
        command.VenueName.Should().Be("Cidade do Rock");
        command.VenueCity.Should().Be("Rio de Janeiro");
        command.VenueLatitude.Should().Be(-22.98);
        command.VenueLongitude.Should().Be(-43.41);
        command.Tags.Should().Contain(new[] { "rock", "festival", "musica" });
        command.SourceTimestampMs.Should().Be(1718000000000);
    }

    [Fact]
    public void ParseEventCommand_WithDeleteOperation_ShouldExtractIdFromBefore()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var json = $$"""
        {
            "payload": {
                "before": {
                    "id": "{{eventId}}",
                    "title": "Cancelled Show"
                },
                "after": null,
                "op": "d",
                "ts_ms": 1718000005000
            }
        }
        """;

        // Act
        var command = CdcMessageParser.ParseEventCommand(json);

        // Assert
        command.Should().NotBeNull();
        command.EventId.Should().Be(eventId);
        command.Operation.Should().Be(CdcOperation.Delete);
        command.Title.Should().Be("Cancelled Show");
        command.SourceTimestampMs.Should().Be(1718000005000);
    }

    [Fact]
    public void ParseTicketTierCommand_WithDebeziumEnvelope_ShouldExtractTierFields()
    {
        // Arrange
        var tierId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var json = $$"""
        {
            "payload": {
                "before": null,
                "after": {
                    "id": "{{tierId}}",
                    "event_id": "{{eventId}}",
                    "name": "Pista Premium",
                    "price": 450.50,
                    "status": "AVAILABLE"
                },
                "op": "c",
                "ts_ms": 1718000010000
            }
        }
        """;

        // Act
        var command = CdcMessageParser.ParseTicketTierCommand(json);

        // Assert
        command.Should().NotBeNull();
        command.TierId.Should().Be(tierId);
        command.EventId.Should().Be(eventId);
        command.Operation.Should().Be(CdcOperation.Create);
        command.TierName.Should().Be("Pista Premium");
        command.Price.Should().Be(450.50m);
        command.Status.Should().Be("AVAILABLE");
        command.SourceTimestampMs.Should().Be(1718000010000);
    }
}
