using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class ScanProfileQueryService(
    ITechnicalScannerDbContext dbContext,
    IndicatorCalculatorRegistry calculatorRegistry)
{
    public IReadOnlyList<ScanProfileDto> GetAll()
    {
        var profiles = dbContext.ScanProfiles
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToList();
        var lookup = BuildCalculationLookup(profiles.SelectMany(DeserializeIndicators).Select(spec => spec.Code));
        return profiles.Select(profile => ToDto(profile, lookup)).ToList();
    }

    public ScanProfileDto? GetByCode(string code)
    {
        var profile = Find(code);
        if (profile is null)
        {
            return null;
        }

        var lookup = BuildCalculationLookup(DeserializeIndicators(profile).Select(spec => spec.Code));
        return ToDto(profile, lookup);
    }

    public ScanProfile? Find(string code)
        => dbContext.ScanProfiles.FirstOrDefault(p => p.Code == code);

    public ScanProfileDto Create(UpsertScanProfileRequest request)
    {
        if (Find(request.Code.Trim().ToUpperInvariant()) is not null)
        {
            throw new InvalidOperationException($"Profile '{request.Code}' already exists.");
        }

        var entity = new ScanProfile
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = request.Description,
            UniverseJson = "{}",
            IndicatorsJson = "[]",
            RuleJson = """{"all":[]}""",
            ScoreWeightsJson = "[]",
            IsActive = request.IsActive
        };

        Apply(entity, request);
        EnsureIndicatorDefinitions(entity);
        dbContext.Add(entity);
        var lookup = BuildCalculationLookup(DeserializeIndicators(entity).Select(spec => spec.Code));
        return ToDto(entity, lookup);
    }

    public ScanProfileDto Update(string code, UpsertScanProfileRequest request)
    {
        var entity = Find(code) ?? throw new InvalidOperationException($"Profile '{code}' was not found.");
        Apply(entity, request);
        entity.UpdatedUtc = DateTimeOffset.UtcNow;
        EnsureIndicatorDefinitions(entity);
        var lookup = BuildCalculationLookup(DeserializeIndicators(entity).Select(spec => spec.Code));
        return ToDto(entity, lookup);
    }

    public ScanProfileDto Deactivate(string code)
    {
        var entity = Find(code) ?? throw new InvalidOperationException($"Profile '{code}' was not found.");
        entity.IsActive = false;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;
        var lookup = BuildCalculationLookup(DeserializeIndicators(entity).Select(spec => spec.Code));
        return ToDto(entity, lookup);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
        => await dbContext.SaveChangesAsync(cancellationToken);

    public void EnsureIndicatorDefinitionsFromProfiles()
    {
        foreach (var profile in dbContext.ScanProfiles.Where(p => p.IsActive).ToList())
        {
            EnsureIndicatorDefinitions(profile);
        }
    }

    private void Apply(ScanProfile entity, UpsertScanProfileRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        entity.UniverseJson = ProfileJson.Serialize(request.Universe ?? new ProfileUniverse());
        var indicators = (request.Indicators ?? [])
            .Select(spec =>
            {
                spec.Code = spec.Code.Trim().ToUpperInvariant();
                return spec;
            })
            .ToList();
        entity.IndicatorsJson = ProfileJson.Serialize(indicators);
        entity.ScoreWeightsJson = ProfileJson.Serialize(
            (request.ScoreWeights ?? [])
                .Select(weight =>
                {
                    weight.IndicatorCode = weight.IndicatorCode.Trim().ToUpperInvariant();
                    return weight;
                })
                .ToList());

        var ruleJson = string.IsNullOrWhiteSpace(request.RuleJson) ? """{"all":[]}""" : request.RuleJson.Trim();
        _ = JsonDocument.Parse(ruleJson);
        entity.RuleJson = ruleJson;
    }

    private void EnsureIndicatorDefinitions(ScanProfile profile)
    {
        var specs = DeserializeIndicators(profile);
        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.Code))
            {
                throw new InvalidOperationException("Each profile indicator must have a code.");
            }

            var existing = dbContext.IndicatorDefinitions.FirstOrDefault(d => d.Code == spec.Code);
            if (existing is not null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(spec.IndicatorType) || string.IsNullOrWhiteSpace(spec.Timeframe))
            {
                throw new InvalidOperationException(
                    $"Indicator '{spec.Code}' does not exist. Provide indicatorType, timeframe, and parameters to create it.");
            }

            _ = calculatorRegistry.GetRequired(spec.IndicatorType);
            var parametersJson = spec.Parameters is { ValueKind: JsonValueKind.Object } parameters
                ? parameters.GetRawText()
                : "{}";
            _ = JsonDocument.Parse(parametersJson);

            dbContext.Add(new IndicatorDefinition
            {
                Code = spec.Code.Trim().ToUpperInvariant(),
                Name = string.IsNullOrWhiteSpace(spec.Name) ? spec.Code : spec.Name,
                IndicatorType = spec.IndicatorType.Trim().ToUpperInvariant(),
                Timeframe = spec.Timeframe.Trim().ToUpperInvariant(),
                ParametersJson = parametersJson,
                NeedsRecalculation = true
            });
        }
    }

    private ScanProfileDto ToDto(ScanProfile profile, CalculationLookup lookup)
    {
        var specs = DeserializeIndicators(profile);
        var missing = specs
            .Where(spec =>
                !lookup.DefinitionIdsByCode.TryGetValue(spec.Code, out var definitionId) ||
                !lookup.DefinitionIdsWithValues.Contains(definitionId))
            .Select(spec => spec.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ScanProfileDto
        {
            Id = profile.Id,
            Code = profile.Code,
            Name = profile.Name,
            Description = profile.Description,
            Universe = DeserializeOrDefault<ProfileUniverse>(profile.UniverseJson) ?? new ProfileUniverse(),
            Indicators = specs,
            RuleJson = profile.RuleJson,
            ScoreWeights = DeserializeOrDefault<List<ScoreWeightSpec>>(profile.ScoreWeightsJson) ?? [],
            IsActive = profile.IsActive,
            MissingCalculationCodes = missing,
            UpdatedUtc = profile.UpdatedUtc
        };
    }

    private CalculationLookup BuildCalculationLookup(IEnumerable<string> codes)
    {
        var codeList = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codeList.Count == 0)
        {
            return new CalculationLookup(
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
                []);
        }

        var definitions = dbContext.IndicatorDefinitions
            .Where(d => codeList.Contains(d.Code))
            .Select(d => new { d.Id, d.Code })
            .ToList();
        var definitionIdsByCode = definitions.ToDictionary(
            d => d.Code,
            d => d.Id,
            StringComparer.OrdinalIgnoreCase);
        var definitionIds = definitions.Select(d => d.Id).ToList();
        var definitionIdsWithValues = definitionIds.Count == 0
            ? new HashSet<long>()
            : dbContext.IndicatorValues
                .Where(v => definitionIds.Contains(v.IndicatorDefinitionId))
                .Select(v => v.IndicatorDefinitionId)
                .Distinct()
                .ToHashSet();

        return new CalculationLookup(definitionIdsByCode, definitionIdsWithValues);
    }

    private sealed record CalculationLookup(
        IReadOnlyDictionary<string, long> DefinitionIdsByCode,
        HashSet<long> DefinitionIdsWithValues);

    private static IReadOnlyList<ProfileIndicatorSpec> DeserializeIndicators(ScanProfile profile)
        => DeserializeOrDefault<List<ProfileIndicatorSpec>>(profile.IndicatorsJson) ?? [];

    private static T? DeserializeOrDefault<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return ProfileJson.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
