using Microsoft.Extensions.Options;

namespace InternLink.Web.Services.AI;

public readonly record struct GeminiKeyLease(int Index, string ApiKey);

public interface IGeminiKeyPool
{
    int KeyCount { get; }

    /// <summary>Round-robins to the next key that is not cooling down.</summary>
    bool TryLease(out GeminiKeyLease lease);

    /// <summary>Parks a key that returned 429/quota-exceeded so the next attempt rotates past it.</summary>
    void ReportQuotaExceeded(int keyIndex);
}

public class GeminiKeyPool : IGeminiKeyPool
{
    private readonly string[] _keys;
    private readonly DateTimeOffset[] _cooldownUntil;
    private readonly TimeSpan _cooldown;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GeminiKeyPool> _logger;
    private readonly object _gate = new();
    private int _cursor;

    public GeminiKeyPool(
        IOptions<GeminiOptions> options,
        TimeProvider timeProvider,
        ILogger<GeminiKeyPool> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;

        var settings = options.Value;
        _keys = ParseKeys(settings.ApiKeys);
        _cooldownUntil = new DateTimeOffset[_keys.Length];
        _cooldown = TimeSpan.FromSeconds(Math.Max(1, settings.KeyCooldownSeconds));

        if (_keys.Length == 0)
        {
            // The app must still boot without keys; AI features fail with a friendly message instead.
            _logger.LogWarning("No usable Gemini API keys configured. Set Gemini:ApiKeys via user-secrets to enable AI features.");
        }
        else
        {
            _logger.LogInformation("Gemini key pool initialised with {KeyCount} key(s).", _keys.Length);
        }
    }

    public int KeyCount => _keys.Length;

    public bool TryLease(out GeminiKeyLease lease)
    {
        lease = default;
        if (_keys.Length == 0)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            for (var offset = 0; offset < _keys.Length; offset++)
            {
                var index = (_cursor + offset) % _keys.Length;
                if (_cooldownUntil[index] > now)
                {
                    continue;
                }

                _cursor = (index + 1) % _keys.Length;
                lease = new GeminiKeyLease(index, _keys[index]);
                return true;
            }
        }

        return false;
    }

    public void ReportQuotaExceeded(int keyIndex)
    {
        if (keyIndex < 0 || keyIndex >= _keys.Length)
        {
            return;
        }

        lock (_gate)
        {
            _cooldownUntil[keyIndex] = _timeProvider.GetUtcNow().Add(_cooldown);
        }

        // Index only — the key value must never reach a log sink.
        _logger.LogWarning(
            "Gemini key #{KeyIndex} exceeded quota; cooling down for {CooldownSeconds}s and rotating.",
            keyIndex,
            _cooldown.TotalSeconds);
    }

    private static string[] ParseKeys(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // Skips the appsettings.json placeholder so an unconfigured checkout behaves as "no keys".
            .Where(k => !k.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
