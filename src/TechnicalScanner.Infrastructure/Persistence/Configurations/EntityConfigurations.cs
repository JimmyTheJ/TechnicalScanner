using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Infrastructure.Persistence.Configurations;

public sealed class TickerConfiguration : IEntityTypeConfiguration<Ticker>
{
    public void Configure(EntityTypeBuilder<Ticker> builder)
    {
        builder.ToTable("ticker");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).IsRequired();
        builder.Property(x => x.Exchange).IsRequired();
        builder.Property(x => x.Country).IsRequired();
        builder.HasIndex(x => new { x.Symbol, x.Exchange, x.Country }).IsUnique();
    }
}

public sealed class PriceCandleConfiguration : IEntityTypeConfiguration<PriceCandle>
{
    public void Configure(EntityTypeBuilder<PriceCandle> builder)
    {
        builder.ToTable("price_candle");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Timeframe).IsRequired();
        builder.Property(x => x.Source).IsRequired();
        builder.HasIndex(x => new { x.TickerId, x.Timeframe, x.CandleDate }).IsUnique();
        builder.HasIndex(x => new { x.TickerId, x.Timeframe, x.CandleDate });
    }
}

public sealed class IndicatorDefinitionConfiguration : IEntityTypeConfiguration<IndicatorDefinition>
{
    public void Configure(EntityTypeBuilder<IndicatorDefinition> builder)
    {
        builder.ToTable("indicator_definition");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb");
    }
}

public sealed class IndicatorValueConfiguration : IEntityTypeConfiguration<IndicatorValue>
{
    public void Configure(EntityTypeBuilder<IndicatorValue> builder)
    {
        builder.ToTable("indicator_value");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ValuesJson).HasColumnName("values").HasColumnType("jsonb");
        builder.HasIndex(x => new { x.TickerId, x.IndicatorDefinitionId, x.CandleDate }).IsUnique();
        builder.HasIndex(x => new { x.TickerId, x.IndicatorDefinitionId, x.CandleDate });
        builder.HasIndex(x => new { x.IndicatorDefinitionId, x.CandleDate, x.PrimaryValue });
    }
}

public sealed class SignalDefinitionConfiguration : IEntityTypeConfiguration<SignalDefinition>
{
    public void Configure(EntityTypeBuilder<SignalDefinition> builder)
    {
        builder.ToTable("signal_definition");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.RuleJson).HasColumnName("rule_json").HasColumnType("jsonb");
    }
}

public sealed class SignalResultConfiguration : IEntityTypeConfiguration<SignalResult>
{
    public void Configure(EntityTypeBuilder<SignalResult> builder)
    {
        builder.ToTable("signal_result");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ValuesJson).HasColumnName("values").HasColumnType("jsonb");
        builder.HasIndex(x => new { x.ScanDateUtc, x.TickerId, x.SignalDefinitionId }).IsUnique();
        builder.HasIndex(x => new { x.ScanDateUtc, x.SignalDefinitionId, x.IsMatch });
        builder.HasIndex(x => new { x.ScanRunId, x.TickerId, x.SignalDefinitionId });
    }
}

public sealed class ScanScoreConfiguration : IEntityTypeConfiguration<ScanScore>
{
    public void Configure(EntityTypeBuilder<ScanScore> builder)
    {
        builder.ToTable("scan_score");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ComponentsJson).HasColumnName("components").HasColumnType("jsonb");
        builder.HasIndex(x => new { x.ScanDateUtc, x.TickerId, x.ScoreCode }).IsUnique();
        builder.HasIndex(x => new { x.ScanRunId, x.TickerId, x.ScoreCode });
    }
}

public sealed class BreadthSnapshotConfiguration : IEntityTypeConfiguration<BreadthSnapshot>
{
    public void Configure(EntityTypeBuilder<BreadthSnapshot> builder)
    {
        builder.ToTable("breadth_snapshot");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ScanRunId, x.MetricCode });
    }
}

public sealed class ScanRunConfiguration : IEntityTypeConfiguration<ScanRun>
{
    public void Configure(EntityTypeBuilder<ScanRun> builder)
    {
        builder.ToTable("scan_run");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.SummaryJson).HasColumnName("summary").HasColumnType("jsonb");
        builder.HasIndex(x => x.StartedUtc);
    }
}

public sealed class TickerOperationFailureConfiguration : IEntityTypeConfiguration<TickerOperationFailure>
{
    public void Configure(EntityTypeBuilder<TickerOperationFailure> builder)
    {
        builder.ToTable("ticker_operation_failure");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).IsRequired();
        builder.Property(x => x.Operation).IsRequired();
        builder.Property(x => x.ErrorMessage).IsRequired();
        builder.HasIndex(x => new { x.ScanRunId, x.Symbol, x.Operation });
    }
}

public sealed class AlertEventConfiguration : IEntityTypeConfiguration<AlertEvent>
{
    public void Configure(EntityTypeBuilder<AlertEvent> builder)
    {
        builder.ToTable("alert_event");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired();
        builder.Property(x => x.SubjectType).IsRequired();
        builder.Property(x => x.SubjectKey).IsRequired();
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.DetailsJson).HasColumnName("details").HasColumnType("jsonb");
        builder.HasIndex(x => x.DetectedUtc);
        builder.HasIndex(x => new { x.EventType, x.SubjectType, x.SubjectKey, x.DetectedUtc });
    }
}

public sealed class ScanProfileConfiguration : IEntityTypeConfiguration<ScanProfile>
{
    public void Configure(EntityTypeBuilder<ScanProfile> builder)
    {
        builder.ToTable("scan_profile");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.UniverseJson).HasColumnName("universe").HasColumnType("jsonb");
        builder.Property(x => x.IndicatorsJson).HasColumnName("indicators").HasColumnType("jsonb");
        builder.Property(x => x.RuleJson).HasColumnName("rule_json").HasColumnType("jsonb");
        builder.Property(x => x.ScoreWeightsJson).HasColumnName("score_weights").HasColumnType("jsonb");
    }
}

public sealed class ProfileBacktestRunConfiguration : IEntityTypeConfiguration<ProfileBacktestRun>
{
    public void Configure(EntityTypeBuilder<ProfileBacktestRun> builder)
    {
        builder.ToTable("profile_backtest_run");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProfileCode).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.HasIndex(x => new { x.ProfileCode, x.StartedUtc });
        builder.HasMany(x => x.Hits)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.ProfileBacktestRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProfileBacktestHitConfiguration : IEntityTypeConfiguration<ProfileBacktestHit>
{
    public void Configure(EntityTypeBuilder<ProfileBacktestHit> builder)
    {
        builder.ToTable("profile_backtest_hit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).IsRequired();
        builder.HasIndex(x => new { x.ProfileBacktestRunId, x.TickerId, x.EntryDate }).IsUnique();
        builder.HasIndex(x => new { x.ProfileBacktestRunId, x.Score });
    }
}

public sealed class AlertDeliveryConfiguration : IEntityTypeConfiguration<AlertDelivery>
{
    public void Configure(EntityTypeBuilder<AlertDelivery> builder)
    {
        builder.ToTable("alert_delivery");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).IsRequired();
        builder.Property(x => x.Destination).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.HasIndex(x => new { x.AlertEventId, x.Channel });
    }
}
