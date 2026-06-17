using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;

namespace TechnicalScanner.Infrastructure.Notifications;

public abstract class HttpAlertChannelBase(IHttpClientFactory httpClientFactory) : IAlertChannel
{
    public abstract string ChannelName { get; }

    public abstract bool IsConfigured { get; }

    public abstract string Destination { get; }

    protected abstract HttpRequestMessage CreateRequest(AlertNotification notification);

    public async Task SendAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(notification);
        var client = httpClientFactory.CreateClient(nameof(HttpAlertChannelBase));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class WebhookAlertChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertsOptions> options) : HttpAlertChannelBase(httpClientFactory)
{
    public override string ChannelName => "Webhook";

    public override bool IsConfigured =>
        options.Value.Channels.Webhook.Enabled &&
        !string.IsNullOrWhiteSpace(options.Value.Channels.Webhook.Url);

    public override string Destination => options.Value.Channels.Webhook.Url;

    protected override HttpRequestMessage CreateRequest(AlertNotification notification)
    {
        var payload = new
        {
            title = notification.Title,
            message = notification.Message,
            events = notification.Events
        };

        return new HttpRequestMessage(HttpMethod.Post, Destination)
        {
            Content = JsonContent.Create(payload)
        };
    }
}

public sealed class DiscordAlertChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertsOptions> options) : HttpAlertChannelBase(httpClientFactory)
{
    public override string ChannelName => "Discord";

    public override bool IsConfigured =>
        options.Value.Channels.Discord.Enabled &&
        !string.IsNullOrWhiteSpace(options.Value.Channels.Discord.WebhookUrl);

    public override string Destination => options.Value.Channels.Discord.WebhookUrl;

    protected override HttpRequestMessage CreateRequest(AlertNotification notification)
    {
        var payload = new
        {
            username = "Technical Scanner",
            content = notification.Message.Length <= 1900
                ? $"**{notification.Title}**\n{notification.Message}"
                : $"**{notification.Title}**\n{notification.Message[..1900]}..."
        };

        return new HttpRequestMessage(HttpMethod.Post, Destination)
        {
            Content = JsonContent.Create(payload)
        };
    }
}

public sealed class SlackAlertChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertsOptions> options) : HttpAlertChannelBase(httpClientFactory)
{
    public override string ChannelName => "Slack";

    public override bool IsConfigured =>
        options.Value.Channels.Slack.Enabled &&
        !string.IsNullOrWhiteSpace(options.Value.Channels.Slack.WebhookUrl);

    public override string Destination => options.Value.Channels.Slack.WebhookUrl;

    protected override HttpRequestMessage CreateRequest(AlertNotification notification)
    {
        var payload = new
        {
            text = $"*{notification.Title}*\n{notification.Message}"
        };

        return new HttpRequestMessage(HttpMethod.Post, Destination)
        {
            Content = JsonContent.Create(payload)
        };
    }
}

public sealed class TelegramAlertChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertsOptions> options) : HttpAlertChannelBase(httpClientFactory)
{
    public override string ChannelName => "Telegram";

    public override bool IsConfigured =>
        options.Value.Channels.Telegram.Enabled &&
        !string.IsNullOrWhiteSpace(options.Value.Channels.Telegram.BotToken) &&
        !string.IsNullOrWhiteSpace(options.Value.Channels.Telegram.ChatId);

    public override string Destination =>
        $"chat:{options.Value.Channels.Telegram.ChatId}";

    protected override HttpRequestMessage CreateRequest(AlertNotification notification)
    {
        var token = options.Value.Channels.Telegram.BotToken;
        var chatId = options.Value.Channels.Telegram.ChatId;
        var text = $"*{notification.Title}*\n{notification.Message}";
        var payload = new
        {
            chat_id = chatId,
            text,
            parse_mode = "Markdown"
        };

        return new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.telegram.org/bot{token}/sendMessage")
        {
            Content = JsonContent.Create(payload)
        };
    }
}

public sealed class EmailAlertChannel(IOptions<AlertsOptions> options) : IAlertChannel
{
    public string ChannelName => "Email";

    public bool IsConfigured
    {
        get
        {
            var email = options.Value.Channels.Email;
            return email.Enabled &&
                   !string.IsNullOrWhiteSpace(email.SmtpHost) &&
                   !string.IsNullOrWhiteSpace(email.FromAddress) &&
                   !string.IsNullOrWhiteSpace(email.ToAddress);
        }
    }

    public string Destination => options.Value.Channels.Email.ToAddress;

    public Task SendAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        var email = options.Value.Channels.Email;
        using var message = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(email.FromAddress),
            Subject = notification.Title,
            Body = notification.Message,
            IsBodyHtml = false
        };
        message.To.Add(email.ToAddress);

        using var client = new System.Net.Mail.SmtpClient(email.SmtpHost, email.SmtpPort)
        {
            EnableSsl = email.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(email.Username)
                ? null
                : new System.Net.NetworkCredential(email.Username, email.Password)
        };

        client.Send(message);
        return Task.CompletedTask;
    }
}
