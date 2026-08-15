using System.Net;
using System.Text;
using System.Text.Json;
using eTicketing.Notifications.Email;
using eTicketing.Notifications.Sending;
using eTicketing.Notifications.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace eTicketing.Notifications.Tests.Sending;

public class BrevoEmailSenderTests
{
    private const string ApiKeySecret = "xkeysib-super-secret-api-key-value";

    private static (BrevoEmailSender Sender, Mock<HttpMessageHandler> Handler, ListLogger<BrevoEmailSender> Logger) BuildSender(
        HttpStatusCode responseStatus)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(responseStatus));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://api.brevo.com/v3/") };
        // Mirrors exactly what NotificationsServiceCollectionExtensions does at DI-registration
        // time — BrevoEmailSender itself never sees this value (see its constructor).
        httpClient.DefaultRequestHeaders.Add("api-key", ApiKeySecret);

        var logger = new ListLogger<BrevoEmailSender>();
        var sender = new BrevoEmailSender(httpClient, new BrevoOptionsSnapshot("no-reply@eticketing.example", "eKarta"), logger);
        return (sender, handlerMock, logger);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_PostsToCorrectEndpointWithApiKeyHeaderAndExpectedBody()
    {
        var (sender, handlerMock, _) = BuildSender(HttpStatusCode.OK);
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedRequest = req;
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await sender.SendAsync(new EmailMessage("jane@example.com", "Jane Doe", "Test Subject", "<p>Body</p>"));

        capturedRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.brevo.com/v3/smtp/email");
        capturedRequest.Headers.GetValues("api-key").Should().ContainSingle().Which.Should().Be(ApiKeySecret);

        using var json = JsonDocument.Parse(capturedBody!);
        json.RootElement.GetProperty("sender").GetProperty("email").GetString().Should().Be("no-reply@eticketing.example");
        json.RootElement.GetProperty("to")[0].GetProperty("email").GetString().Should().Be("jane@example.com");
        json.RootElement.GetProperty("subject").GetString().Should().Be("Test Subject");
        json.RootElement.GetProperty("htmlContent").GetString().Should().Be("<p>Body</p>");
    }

    [Fact]
    public async Task SendAsync_OnNonSuccessStatus_Throws()
    {
        var (sender, _, _) = BuildSender(HttpStatusCode.InternalServerError);

        var act = async () => await sender.SendAsync(new EmailMessage("jane@example.com", null, "Subject", "<p>Body</p>"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAsync_OnFailure_LoggedMessageNeverContainsApiKeyValue()
    {
        var (sender, _, logger) = BuildSender(HttpStatusCode.InternalServerError);

        try
        {
            await sender.SendAsync(new EmailMessage("jane@example.com", null, "Subject", "<p>Body</p>"));
        }
        catch (InvalidOperationException)
        {
            // Expected — the assertion below is what this test actually checks.
        }

        logger.Messages.Should().NotContain(m => m.Contains(ApiKeySecret));
    }
}
