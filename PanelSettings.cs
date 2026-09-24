using System;
using System.Collections.Generic;
using System.IO;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin
{
    internal enum ArtworkPosition
    {
        None,
        Above,
        Below,
        Left,
        Right,
    }

    internal enum TrackSource
    {
        PlayingTrack,
        SelectedTrack,
    }

    // Panel configuration, editable via the panel header's right-click "Settings..."
    // menu (Plugin.GetMenuItems). Persisted as a plain key=value text file.
    internal static class PanelSettings
    {
        public const int RowCount = 7;

        public static ArtworkPosition ArtworkPosition = ArtworkPosition.Below;
        public static bool ShowRating = true;
        public static bool ShowTrackProperties = false;
        public static TrackSource TrackSource = TrackSource.PlayingTrack;

        // Each row is free text using MusicBee-style <Tag> placeholders, e.g.
        // "<Title>" or "by <Artist>". Empty rows are hidden. Multi-value tags
        // (Artist, Genre) are split into individually clickable segments.
        public static string[] Rows = new[]
        {
            "<Title>",
            "by <Artist>",
            "on <Album>",
            "<Genre>",
            "",
            "",
            "",
        };

        private static string SettingsPath =>
            Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "Information", "settings.txt");

        public static void Load()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return;

                var values = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    values[line.Substring(0, eq)] = line.Substring(eq + 1);
                }

                if (values.TryGetValue(nameof(ArtworkPosition), out string ap) && Enum.TryParse(ap, out ArtworkPosition parsedAp))
                    ArtworkPosition = parsedAp;

                if (values.TryGetValue(nameof(ShowRating), out string sr) && bool.TryParse(sr, out bool parsedSr))
                    ShowRating = parsedSr;

                if (values.TryGetValue(nameof(ShowTrackProperties), out string stp) && bool.TryParse(stp, out bool parsedStp))
                    ShowTrackProperties = parsedStp;

                if (values.TryGetValue(nameof(TrackSource), out string ts) && Enum.TryParse(ts, out TrackSource parsedTs))
                    TrackSource = parsedTs;

                for (int i = 0; i < RowCount; i++)
                {
                    if (values.TryGetValue($"Row{i + 1}", out string rowValue))
                        Rows[i] = rowValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading Information settings", ex);
            }
        }

        public static void Save()
        {
            try
            {
                string path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var lines = new List<string>
                {
                    $"{nameof(ArtworkPosition)}={ArtworkPosition}",
                    $"{nameof(ShowRating)}={ShowRating}",
                    $"{nameof(ShowTrackProperties)}={ShowTrackProperties}",
                    $"{nameof(TrackSource)}={TrackSource}",
                };
                for (int i = 0; i < RowCount; i++)
                    lines.Add($"Row{i + 1}={Rows[i]}");

                File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving Information settings", ex);
            }
        }
    }
}
