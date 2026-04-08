using System.Globalization;
using AIPort.Adapter.Orchestrator.Services.Interfaces;

namespace AIPort.Adapter.Orchestrator.Services;

public sealed class SystemTelemetryProvider : ISystemTelemetryProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan InitialCpuSampleDelay = TimeSpan.FromMilliseconds(150);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SystemTelemetrySnapshot? _cachedSnapshot;
    private DateTime _cacheExpiresAtUtc = DateTime.MinValue;
    private LinuxCpuSample? _lastLinuxCpuSample;

    public async Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cachedSnapshot;
        if (cached is not null && DateTime.UtcNow < _cacheExpiresAtUtc)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cached = _cachedSnapshot;
            if (cached is not null && DateTime.UtcNow < _cacheExpiresAtUtc)
            {
                return cached;
            }

            var snapshot = OperatingSystem.IsLinux()
                ? await CaptureLinuxSnapshotAsync(cancellationToken)
                : CaptureFallbackSnapshot();

            _cachedSnapshot = snapshot;
            _cacheExpiresAtUtc = DateTime.UtcNow.Add(CacheDuration);
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SystemTelemetrySnapshot> CaptureLinuxSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cpuUsagePercent = await ReadLinuxCpuUsagePercentAsync(cancellationToken);
            var memoryInfo = await ReadLinuxMemoryInfoAsync(cancellationToken);

            return new SystemTelemetrySnapshot(
                Status: cpuUsagePercent.HasValue && memoryInfo.TotalBytes.HasValue ? "healthy" : "degraded",
                Platform: "linux",
                SampledAtUtc: DateTime.UtcNow,
                LogicalCores: Environment.ProcessorCount,
                CpuUsagePercent: cpuUsagePercent,
                TotalMemoryBytes: memoryInfo.TotalBytes,
                UsedMemoryBytes: memoryInfo.UsedBytes,
                AvailableMemoryBytes: memoryInfo.AvailableBytes,
                MemoryUsagePercent: memoryInfo.UsagePercent,
                Message: "Telemetria coletada do host Linux via /proc.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SystemTelemetrySnapshot(
                Status: "degraded",
                Platform: "linux",
                SampledAtUtc: DateTime.UtcNow,
                LogicalCores: Environment.ProcessorCount,
                CpuUsagePercent: null,
                TotalMemoryBytes: null,
                UsedMemoryBytes: null,
                AvailableMemoryBytes: null,
                MemoryUsagePercent: null,
                Message: $"Falha ao coletar telemetria do host: {ex.Message}");
        }
    }

    private SystemTelemetrySnapshot CaptureFallbackSnapshot()
    {
        var process = Environment.ProcessId;
        return new SystemTelemetrySnapshot(
            Status: "degraded",
            Platform: Environment.OSVersion.Platform.ToString().ToLowerInvariant(),
            SampledAtUtc: DateTime.UtcNow,
            LogicalCores: Environment.ProcessorCount,
            CpuUsagePercent: null,
            TotalMemoryBytes: null,
            UsedMemoryBytes: null,
            AvailableMemoryBytes: null,
            MemoryUsagePercent: null,
            Message: $"Telemetria de host completa nao esta disponivel nesta plataforma. Processo atual: {process}.");
    }

    private async Task<double?> ReadLinuxCpuUsagePercentAsync(CancellationToken cancellationToken)
    {
        var current = await ReadLinuxCpuSampleAsync(cancellationToken);
        var baseline = _lastLinuxCpuSample;

        if (baseline is null || current.TotalTicks <= baseline.TotalTicks)
        {
            baseline = current;
            await Task.Delay(InitialCpuSampleDelay, cancellationToken);
            current = await ReadLinuxCpuSampleAsync(cancellationToken);
        }

        _lastLinuxCpuSample = current;

        var totalDelta = current.TotalTicks - baseline.TotalTicks;
        var idleDelta = current.IdleTicks - baseline.IdleTicks;

        if (totalDelta <= 0)
        {
            return null;
        }

        var usagePercent = (1d - ((double)idleDelta / totalDelta)) * 100d;
        return Math.Round(Math.Clamp(usagePercent, 0d, 100d), 1, MidpointRounding.AwayFromZero);
    }

    private static async Task<LinuxCpuSample> ReadLinuxCpuSampleAsync(CancellationToken cancellationToken)
    {
        var line = await ReadFirstLineAsync("/proc/stat", cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException("/proc/stat nao retornou dados.");
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || !string.Equals(parts[0], "cpu", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Formato inesperado em /proc/stat.");
        }

        long totalTicks = 0;
        for (var index = 1; index < parts.Length; index++)
        {
            totalTicks += long.Parse(parts[index], CultureInfo.InvariantCulture);
        }

        var idleTicks = long.Parse(parts[4], CultureInfo.InvariantCulture);
        if (parts.Length > 5)
        {
            idleTicks += long.Parse(parts[5], CultureInfo.InvariantCulture);
        }

        return new LinuxCpuSample(totalTicks, idleTicks);
    }

    private static async Task<(long? TotalBytes, long? UsedBytes, long? AvailableBytes, double? UsagePercent)> ReadLinuxMemoryInfoAsync(CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync("/proc/meminfo", cancellationToken);
        long? totalKb = null;
        long? availableKb = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                totalKb = ParseMemInfoValueKb(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                availableKb = ParseMemInfoValueKb(line);
            }

            if (totalKb.HasValue && availableKb.HasValue)
            {
                break;
            }
        }

        if (!totalKb.HasValue)
        {
            return (null, null, null, null);
        }

        var totalBytes = totalKb.Value * 1024L;
        var availableBytes = Math.Max(0L, (availableKb ?? 0L) * 1024L);
        var usedBytes = Math.Max(0L, totalBytes - availableBytes);
        double? usagePercent = totalBytes == 0
            ? null
            : Math.Round(((double)usedBytes / totalBytes) * 100d, 1, MidpointRounding.AwayFromZero);

        return (totalBytes, usedBytes, availableBytes, usagePercent);
    }

    private static long ParseMemInfoValueKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Formato inesperado em /proc/meminfo: {line}");
        }

        return long.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ReadFirstLineAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadLineAsync(cancellationToken);
    }

    private sealed record LinuxCpuSample(long TotalTicks, long IdleTicks);
}