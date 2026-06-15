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
    }
}

public sealed class BreadthSnapshotConfiguration : IEntityTypeConfiguration<BreadthSnapshot>
{
    public void Configure(EntityTypeBuilder<BreadthSnapshot> builder)
    {
        builder.ToTable("breadth_snapshot");
        builder.HasKey(x => x.Id);
    }
}
