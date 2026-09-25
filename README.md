# Information

A MusicBee plugin that replaces the built-in Track & Artist panel with one where
individual artists, genres and the album are clickable, plus a few things the native
panel doesn't offer (an editable rating, per-selection multi-track display, more
flexible row templates).

## Setup

1. Close MusicBee and copy `mb_Information.dll` into MusicBee's `Plugins` folder
   (`%AppData%\MusicBee\Plugins`, or the `Plugins` folder next to `MusicBee.exe` for a
   portable install). Then start MusicBee.
2. Open MusicBee's Preferences > **Layout** tab > **Arrange Panels**.
3. Find "Information" in the list of available elements and enable it (it's typically
   used in place of, or alongside, the native Track & Artist panel).
4. Right-click the panel's header (or use its "..." button in Arrange Panels) for
   **Playing Track** / **Selected Track** and **Settings...**.

## What it does

- Shows up to 7 configurable text rows built from MusicBee-style `<Tag>` placeholders
  (e.g. `by <Artist>`, `on <Album>`), using MusicBee's own display field names (e.g.
  `<Title>`, `<Album Artist>`, `<Full Year>`) - the API's enum names (`<TrackTitle>`,
  `<AlbumArtist>`, ...) work too as a fallback. Empty rows are hidden. A row can also
  use file-property placeholders that aren't taggable fields: `<Type>`, `<Sample
  Rate>`, `<Bitrate>`, `<Bit Depth>`, `<Channels>`, `<Duration>`/`<Time>`, `<Size>`,
  `<Date Added>`, `<Date Modified>`, `<Last Played>`, `<Play Count>`, `<Skip Count>`.
- Multi-value tags (Artist, Genre) are split so every individual value is its own
  segment. Hovering a segment underlines just that value; clicking it browses to that
  value in MusicBee's **Thumbnail Browser** panel if one is open (like clicking it
  there yourself), or otherwise opens a new tab filtered to it. Ctrl+click always opens
  a filtered tab instead. The album value filters by album + album artist together.
  The title is shown bold and, like in the native panel, isn't clickable.
- Artwork can be shown **Above**, **Below**, **Left**, **Right** of the text, or not at
  all. Double-click it to open MusicBee's own full-size artwork viewer. Right-click
  shows MusicBee's native artwork context menu when it can be found; otherwise a small
  built-in menu (Show Full Size / Copy / Save As) covers the same core actions.
- An editable 5-star rating strip (half-star precision), drawn to match the native
  panel. Changing it writes the rating to the file's tags immediately and refreshes
  MusicBee's other panels.
- An optional track properties line, like the native panel's: type, sample rate,
  bitrate, channels and duration (e.g. "FLAC 44.1kHz, 918kbps, Stereo, 4:26").
- **Playing Track** or **Selected Track** as the source (from the header menu), same
  choice the native panel offers. With several tracks selected, only what they have in
  common is shown: a row appears only if every selected track has the same value (so
  selecting a whole album hides the title but keeps album/artist/year), multi-value
  tags show the values shared by all of them, duration sums to a total instead, and
  artwork only shows when the whole selection belongs to one album. Changing the
  rating from the panel applies it to every selected track.
- **Settings...** (header right-click, or Arrange Panels' "...") edits the 7 row
  templates, the artwork position, and whether the rating and properties line are
  shown. Saved as plain text in MusicBee's data folder
  (`...\MusicBee\Information\settings.txt`, or the portable install's own data
  folder).
- Sizes itself to MusicBee's own (skinned) panel scrollbar when hosted in a
  scrollable container, matching how the native panel scrolls; falls back to a
  standard scrollbar if that host isn't recognized.

## Known limitations

- Several pieces of this plugin rely on MusicBee internals rather than the plugin API
  (found by decompiling MusicBee.exe v3.6), since there's no public API for them:
  selected-file notifications, the header's right-click menu, driving the Thumbnail
  Browser panel, native scroll-host sizing, and opening the native full-size artwork
  viewer. A MusicBee update can break any of these. Each one fails gracefully instead
  of crashing the panel (a filtered tab opens instead of browsing the Thumbnail
  Browser, a standard scrollbar is used, a fallback artwork menu is shown, etc.) and
  logs why to `Information\error.log` in MusicBee's data folder.
- There's no documented way to share settings or state with MusicBee's native Track &
  Artist panel - this plugin keeps its own separate configuration.
- Row templates need MusicBee's own display field names (e.g. `Full Year`, not
  `Year`); an unrecognized `<Tag>` is left as-is in the row so a typo is easy to spot.
- The `<Type>` placeholder can come back empty for some formats (seen on FLAC files);
  it then falls back to the file extension.
- Only display and rating editing are covered here. Building filter rules (numeric
  ranges, AND/OR conditions, ...) is the Filter Panel plugin's job, not this one's.
