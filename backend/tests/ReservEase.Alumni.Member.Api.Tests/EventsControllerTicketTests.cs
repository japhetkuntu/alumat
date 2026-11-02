using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Controllers;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>The paid-RSVP endpoint passes everything the member chose on to checkout. The service tests cover what checkout does with it.</summary>
public class EventsControllerTicketTests
{
    private static (EventsController Controller, Mock<IEventTicketService> Tickets) Build()
    {
        var tickets = new Mock<IEventTicketService>();
        tickets.Setup(t => t.InitiateAsync(It.IsAny<EventTicketRequest>(), It.IsAny<AuthData>()))
            .ReturnsAsync(new EventTicketCheckoutResponse { Reference = "ET_x" }.ToOkApiResponse());
        var controller = new EventsController(Mock.Of<IMemberEventService>(), tickets.Object, Mock.Of<IMemberEventUpdateService>());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "m1"), new Claim(ClaimTypes.Email, "ama@x.com"),
            new Claim(ClaimTypes.GivenName, "Ama"), new Claim(ClaimTypes.Surname, "Mensah"), new Claim(ClaimTypes.Role, "Member")], "test");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return (controller, tickets);
    }

    [Fact]
    public async Task The_chosen_price_and_the_return_address_reach_checkout()
    {
        var (controller, tickets) = Build();

        await controller.BuyTicket("e1", new EventTicketBody("https://app.test/events/ticket-status", "opt-cert"));

        tickets.Verify(t => t.InitiateAsync(
            It.Is<EventTicketRequest>(r => r.EventId == "e1" && r.OptionId == "opt-cert" && r.CallbackUrl == "https://app.test/events/ticket-status"),
            It.IsAny<AuthData>()), Times.Once);
    }

    [Fact]
    public async Task A_request_with_no_body_still_reaches_checkout_with_no_price_chosen()
    {
        var (controller, tickets) = Build();

        await controller.BuyTicket("e1", null);

        tickets.Verify(t => t.InitiateAsync(It.Is<EventTicketRequest>(r => r.EventId == "e1" && r.OptionId == null), It.IsAny<AuthData>()), Times.Once);
    }
}
