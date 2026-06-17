namespace TechnicalScanner.Application.Options;

public sealed class AlertsOptions
{
    public const string SectionName = "Alerts";

    public bool Enabled { get; set; }

    public int MinMinutesBetweenDuplicates { get; set; } = 60;

    public bool NotifyOnSignalFired { get; set; } = true;

    public bool NotifyOnSignalCleared { get; set; }

    public decimal CompositeScoreThreshold { get; set; } = 4;

    public string CompositeScoreCode { get; set; } = "DEEP_PULLBACK";

    public List<string> SignalCodes { get; set; } = [];

    public List<BreadthAlertRuleOptions> BreadthRules { get; set; } = [];

    public AlertChannelsOptions Channels { get; set; } = new();
}

public sealed class BreadthAlertRuleOptions
{
    public string MetricCode { get; set; } = string.Empty;

    public decimal Threshold { get; set; }

    public string Direction { get; set; } = "CrossBelow";
}

public sealed class AlertChannelsOptions
{
    public WebhookChannelOptions Webhook { get; set; } = new();

    public EmailChannelOptions Email { get; set; } = new();

    public TelegramChannelOptions Telegram { get; set; } = new();

    public DiscordChannelOptions Discord { get; set; } = new();

    public SlackChannelOptions Slack { get; set; } = new();
}

public sealed class WebhookChannelOptions
{
    public bool Enabled { get; set; }

    public string Url { get; set; } = string.Empty;
}

public sealed class EmailChannelOptions
{
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string ToAddress { get; set; } = string.Empty;
}

public sealed class TelegramChannelOptions
{
    public bool Enabled { get; set; }

    public string BotToken { get; set; } = string.Empty;

    public string ChatId { get; set; } = string.Empty;
}

public sealed class DiscordChannelOptions
{
    public bool Enabled { get; set; }

    public string WebhookUrl { get; set; } = string.Empty;
}

public sealed class SlackChannelOptions
{
    public bool Enabled { get; set; }

    public string WebhookUrl { get; set; } = string.Empty;
}
