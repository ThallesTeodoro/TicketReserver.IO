namespace TR.API.Reservation.UnitTests.Endpoints;

using Carter;
using Microsoft.AspNetCore.Routing;
using Moq;
using TR.API.Reservation.WebApi.Endpoints;

public class ReservationEndpointsTests
{
    [Fact]
    public void ReservationEndpoints_ShouldImplementICarterModule()
    {
        var endpoints = new ReservationEndpoints();
        Assert.IsAssignableFrom<ICarterModule>(endpoints);
    }

    [Fact]
    public void HoldReservationRequest_ShouldBeConstructedCorrectly()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        var item = new HoldItemRequest(tierId, seatId, "A-1", 1, 150m);
        var request = new HoldReservationRequest(customerId, eventId, new[] { item }, "BRL");

        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal(eventId, request.EventId);
        Assert.Single(request.Items);
        Assert.Equal("BRL", request.Currency);
    }

    [Fact]
    public void CheckoutReservationRequest_ShouldBeConstructedCorrectly()
    {
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var payment = new CheckoutPaymentDetailsRequest("CREDIT_CARD", "tok_123", 2, "12345678900");

        var request = new CheckoutReservationRequest(reservationId, customerId, payment);

        Assert.Equal(reservationId, request.ReservationId);
        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal("CREDIT_CARD", request.PaymentDetails.PaymentMethod);
        Assert.Equal(2, request.PaymentDetails.Installments);
    }

    [Fact]
    public void CancelReservationRequest_ShouldSupportDefaultValues()
    {
        var request = new CancelReservationRequest();

        Assert.Null(request.CustomerId);
        Assert.Null(request.Reason);
    }
}
