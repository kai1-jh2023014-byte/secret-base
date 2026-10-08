using System.Text.Json;
using SecretBase.Core.Progress;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Progress;

namespace SecretBase.Core.Tests;

public class ProgressGenesisFormatterTests
{
    [Fact]
    public void FormatPercent_ClampsAndFormats()
    {
        Assert.Equal("0%", ProgressGenesisFormatter.FormatPercent(-5));
        Assert.Equal("100%", ProgressGenesisFormatter.FormatPercent(140));
        Assert.Equal("42.5%", ProgressGenesisFormatter.FormatPercent(42.5));
    }

    [Fact]
    public void FormatHeadlines_IncludeTitlesAndStages()
    {
        var progress = new ProgressTrack { Title = "Progress", Percent = 28, Status = "On track" };
        var genesis = new GenesisTrack
        {
            Title = "Genesis",
            Phase = "Foundation",
            Stage = 2,
            StageCount = 5,
            Percent = 35,
            Milestones =
            [
                GenesisMilestone.Create("A", isComplete: true),
                GenesisMilestone.Create("B", isComplete: false)
            ]
        };

        Assert.Equal("Progress · 28%", ProgressGenesisFormatter.FormatProgressHeadline(progress));
        Assert.Equal(
            "Genesis · Foundation · Stage 2/5",
            ProgressGenesisFormatter.FormatGenesisHeadline(genesis));
        Assert.Equal("35% · 1/2 milestones", ProgressGenesisFormatter.FormatGenesisPercentLine(genesis));
    }

    [Fact]
    public void FormatUpdatedAt_UsesRelativeWindows()
    {
        var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("Updated just now", ProgressGenesisFormatter.FormatUpdatedAt(now, now));
        Assert.Equal(
            "Updated 5m ago",
            ProgressGenesisFormatter.FormatUpdatedAt(now.AddMinutes(-5), now));
        Assert.Equal(
            "Updated 2h ago",
            ProgressGenesisFormatter.FormatUpdatedAt(now.AddHours(-2), now));
    }

    [Fact]
    public void FormatSourceCaption_MapsKnownKinds()
    {
        Assert.Equal("Source · local JSON", ProgressGenesisFormatter.FormatSourceCaption(null));
        Assert.Equal(
            "Source · HTTP JSON",
            ProgressGenesisFormatter.FormatSourceCaption(ProgressGenesisSourceKinds.Http));
    }
}

public class ProgressGenesisModelTests
{
    [Fact]
    public void Normalize_ClampsPercentsAndStages()
    {
        var snapshot = new ProgressGenesisSnapshot
        {
            Progress = new ProgressTrack { Percent = 250, Title = "  " },
            Genesis = new GenesisTrack
            {
                Stage = 99,
                StageCount = 0,
                Percent = -10,
                Milestones =
                [
                    new GenesisMilestone { Label = "  Keep  ", IsComplete = true },
                    new GenesisMilestone { Label = "   ", IsComplete = false }
                ]
            }
        };

        snapshot.Normalize();

        Assert.Equal("Progress", snapshot.Progress.Title);
        Assert.Equal(100, snapshot.Progress.Percent);
        Assert.Equal(1, snapshot.Genesis.StageCount);
        Assert.Equal(1, snapshot.Genesis.Stage);
        Assert.Equal(0, snapshot.Genesis.Percent);
        Assert.Single(snapshot.Genesis.Milestones);
        Assert.Equal("Keep", snapshot.Genesis.Milestones[0].Label);
    }

    [Fact]
    public void DemoSeed_HasUsableProgressAndGenesis()
    {
        var seed = ProgressGenesisSnapshot.CreateDemoSeed();
        Assert.True(seed.Progress.Percent > 0);
        Assert.True(seed.Genesis.Milestones.Count >= 2);
        Assert.Equal(ProgressGenesisSourceKinds.LocalJson, seed.SourceKind);
    }

    [Fact]
    public void MemoryProvider_RoundTripsSave()
    {
        var provider = new MemoryProgressGenesisProvider();
        var snapshot = provider.LoadOrCreate();
        snapshot.Progress.Percent = 77;
        snapshot.Genesis.Phase = "Expansion";
        provider.Save(snapshot);

        var restored = provider.LoadOrCreate();
        Assert.Equal(77, restored.Progress.Percent);
        Assert.Equal("Expansion", restored.Genesis.Phase);
        Assert.Equal(ProgressGenesisSourceKinds.Memory, restored.SourceKind);
    }
}

public class ProgressWidgetConfigurationTests
{
    [Fact]
    public void RoundTripsDictionary()
    {
        var config = new ProgressWidgetConfiguration
        {
            Source = ProgressGenesisSources.Http,
            RemoteUrl = "https://example.com/progress-genesis.json",
            DisplayMode = ProgressWidgetConfiguration.DisplayMinimal,
            RefreshSeconds = 90,
            ShowMilestones = false
        };

        var roundTrip = ProgressWidgetConfiguration.FromDictionary(config.ToDictionary());
        Assert.Equal(config.RemoteUrl, roundTrip.RemoteUrl);
        Assert.Equal(ProgressWidgetConfiguration.DisplayMinimal, roundTrip.DisplayMode);
        Assert.True(roundTrip.IsMinimal);
        Assert.Equal(90, roundTrip.RefreshSeconds);
        Assert.False(roundTrip.ShowMilestones);
    }

    [Fact]
    public void NormalizeDisplayMode_AcceptsTransparentAliases()
    {
        Assert.Equal(
            ProgressWidgetConfiguration.DisplayMinimal,
            ProgressWidgetConfiguration.NormalizeDisplayMode("transparent"));
        Assert.Equal(
            ProgressWidgetConfiguration.DisplayFull,
            ProgressWidgetConfiguration.NormalizeDisplayMode(null));
    }

    [Fact]
    public void ClampRefreshSeconds_EnforcesBounds()
    {
        Assert.Equal(15, ProgressWidgetConfiguration.ClampRefreshSeconds(1));
        Assert.Equal(600, ProgressWidgetConfiguration.ClampRefreshSeconds(9999));
    }

    [Fact]
    public void FromDictionary_IgnoresEmptyRemoteUrl()
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [nameof(ProgressWidgetConfiguration.RemoteUrl)] = JsonSerializer.SerializeToElement("  ")
        };
        var config = ProgressWidgetConfiguration.FromDictionary(dict);
        Assert.Null(config.RemoteUrl);
    }
}

public class ProgressWidgetCatalogTests
{
    [Fact]
    public void Catalog_IncludesProgressGenesis()
    {
        Assert.Contains(WidgetCatalog.Entries, e => e.WidgetType == WidgetTypes.Progress);
        var entry = WidgetCatalog.FindById("progress");
        Assert.NotNull(entry);
        Assert.Equal("Progress / Genesis", entry!.Label);
        Assert.Equal(WidgetCatalogGroups.Information, entry.Group);
    }

    [Fact]
    public void Factory_CreatesProgressWidget()
    {
        var widget = DefaultWidgetFactory.CreateProgress();
        Assert.Equal(WidgetTypes.Progress, widget.Type);
        Assert.Equal(320, widget.Size.Width);
        Assert.Equal(300, widget.Size.Height);
        Assert.Contains(nameof(ProgressWidgetConfiguration.RefreshSeconds), widget.Configuration.Keys);
    }
}
