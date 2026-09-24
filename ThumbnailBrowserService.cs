using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;
using static MusicBeePlugin.ReflectionHelper;

namespace MusicBeePlugin
{
    // Drives MusicBee's built-in "Thumbnail Browser" panel the same way a user
    // would: (1) pick the category (Artist/Genre/Year...) from the panel header's
    // own drop-down menu, then (2) select the matching entry in the list, which
    // filters the main panel. MusicBee has no public API for this, so everything
    // here was found by decompiling MusicBee.exe (v3.6) - see the notes on each
    // step. Member names are obfuscated but stable within a MusicBee build; each
    // lookup is also checked against its expected signature, and on any mismatch
    // this logs what failed and returns false so the caller opens a filtered tab
    // instead.
    internal static class ThumbnailBrowserService
    {
        // Main form: static "current tab" state object.
        private const string CurrentTabField = "#=zJgnXV15D9J4B";
        // Tab state: that tab's Thumbnail Browser control (null if not in layout).
        private const string TabBrowserField = "#=zXc$O5F9HVY2u";
        // Browser: struct copy of the tab's browse settings; its first field is the category (TagCode).
        private const string BrowserSettingsField = "#=z0Bs1pAc=";
        private const string SettingsCategoryField = "#=zrbFJ8WU=";
        // Browser: the inner thumbnail list control.
        private const string BrowserListField = "#=z$UqHjL_IOTxO";
        // Browser: select(value, notify) - normalises the value like MusicBee's own
        // sort keys; notify=true raises the same event as a user click (filters
        // the main panel); notify=false selects only an exact match, silently.
        private const string SelectValueMethod = "#=z7s_GYqHmn9Bm";
        // Browser: currently selected entry key, or null for none/"All".
        private const string SelectedValueMethod = "#=zTYUhtK4_qRn3";
        // Inner list: selected index, ensure-row-visible(row), items per row.
        private const string ListSelectedIndexMethod = "#=zWCoeRc4WhWuM";
        private const string ListEnsureRowVisibleMethod = "#=zUoXjOFxTWtqd";
        private const string ListItemsPerRowField = "#=zNxDG8E2eODmL";
        // Inner list, for albums (see SelectAlbum): entries, entry key, album id
        // -> track map, track get-tag(TagCode), select(index, userInitiated, notify).
        private const string ListItemsField = "#=zUXm5OgtwMVJJ";
        private const string ListItemKeyField = "#=zhYEfJLE=";
        private const string ListAlbumTracksField = "#=zdLmHsDRmvIgOjEd0kA==";
        private const string TrackGetTagMethod = "#=zn_A54M8SH1hf";
        private const string ListSelectIndexMethod = "#=zvfWTxgP$2vtI";

        private const int RebuildTimeoutMs = 5000;

        private static Type _tagCodeType;

        public static async Task<bool> TryBrowseToAsync(TrackInfoSegment segment)
        {
            MetaDataType field = segment.Field1;

            try
            {
                // no (visible) Thumbnail Browser in the current tab, or no category for this field
                Control browser = GetCurrentTabBrowser();
                if (browser == null || !browser.Visible || browser.Width <= 0 || browser.Height <= 0)
                    return false;

                if (!TryMapCategory(field, out object tagCode))
                    return false;

                if (!Equals(GetCurrentCategory(browser), tagCode))
                {
                    if (!ClickCategoryMenuItem(browser, tagCode))
                        return false;

                    if (!await WaitForListRebuild(browser))
                    {
                        Logger.Info("[ThumbnailBrowser] Timed out waiting for the list to rebuild.");
                        return false;
                    }

                    if (!Equals(GetCurrentCategory(browser), tagCode))
                    {
                        Logger.Info($"[ThumbnailBrowser] Category did not change to {tagCode}.");
                        return false;
                    }
                }

                if (field == MetaDataType.Album)
                    return SelectAlbum(browser, segment.Value1, segment.Value2, segment.AlbumId);

                return SelectValue(browser, segment.Value1);
            }
            catch (Exception ex)
            {
                Logger.Error("Error driving Thumbnail Browser panel", ex);
                return false;
            }
        }

        private static Control GetCurrentTabBrowser()
        {
            var mainForm = MainWindow;
            if (mainForm == null) return null;

            var tabField = FindField(mainForm.GetType(), CurrentTabField, AnyStatic);
            object tab = tabField?.GetValue(null);
            if (tab == null)
            {
                Logger.Info($"[ThumbnailBrowser] Current tab field {CurrentTabField} not found on {mainForm.GetType().FullName}.");
                return null;
            }

            var browserField = FindField(tab.GetType(), TabBrowserField, AnyInstance);
            if (browserField == null || !typeof(Control).IsAssignableFrom(browserField.FieldType))
            {
                Logger.Info($"[ThumbnailBrowser] Tab browser field {TabBrowserField} not found on {tab.GetType().FullName}.");
                return null;
            }

            return browserField.GetValue(tab) as Control;
        }

        // Plugin MetaDataType codes match MusicBee's internal TagCode values for
        // most fields, but names are more reliable. The browser's year category
        // is YearOnly (Year is the full date).
        private static bool TryMapCategory(MetaDataType field, out object tagCode)
        {
            tagCode = null;
            if (_tagCodeType == null)
            {
                _tagCodeType = MusicBeeAssembly?.GetType("MusicBee.Media.TagCode", false);
                if (_tagCodeType == null)
                {
                    Logger.Info("[ThumbnailBrowser] MusicBee.Media.TagCode type not found.");
                    return false;
                }
            }

            string name = field == MetaDataType.Year ? "YearOnly" : field.ToString();
            try
            {
                tagCode = Enum.Parse(_tagCodeType, name, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetCurrentCategory(Control browser)
        {
            var settingsField = FindField(browser.GetType(), BrowserSettingsField, AnyInstance);
            object settings = settingsField?.GetValue(browser);
            if (settings == null) return null;

            return FindField(settings.GetType(), SettingsCategoryField, AnyInstance)?.GetValue(settings);
        }

        // Step 1: MusicBee's own header drop-down builds its category items once
        // (lazily, the first time the header menu is opened) - each item's Tag is
        // the TagCode and its Click handler updates the tab's browse settings and
        // rebuilds the list. PerformClick on that same item = the user picking it.
        private static bool ClickCategoryMenuItem(Control browser, object tagCode)
        {
            var item = FindCategoryMenuItem(browser, tagCode);
            if (item == null)
            {
                // Not built yet: ask the browser to build its header menu, exactly
                // like opening it does (but without showing it).
                BuildHeaderMenu(browser);
                item = FindCategoryMenuItem(browser, tagCode);
            }

            if (item == null)
            {
                Logger.Info($"[ThumbnailBrowser] No header menu item for category {tagCode}.");
                return false;
            }

            item.PerformClick();
            return true;
        }

        // The category items live in a ToolStripItem[] field on the browser. The
        // "Group By" submenu items also carry TagCodes, so pick the smallest array
        // whose top-level items include Album (which only the category list has),
        // then search it and its submenus (the "more categories" entries).
        private static ToolStripMenuItem FindCategoryMenuItem(Control browser, object tagCode)
        {
            object albumTag = Enum.Parse(_tagCodeType, "Album");

            ToolStripItem[] categoryItems = GetAllFields(browser.GetType(), AnyInstance)
                .Where(f => f.FieldType == typeof(ToolStripItem[]))
                .Select(f => f.GetValue(browser) as ToolStripItem[])
                .Where(a => a != null && a.Any(i => Equals(i?.Tag, albumTag)))
                .OrderBy(a => a.Length)
                .FirstOrDefault();

            if (categoryItems == null) return null;

            return FindItemByTag(categoryItems, tagCode);
        }

        private static ToolStripMenuItem FindItemByTag(IEnumerable<ToolStripItem> items, object tagCode)
        {
            foreach (var item in items)
            {
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (Equals(menuItem.Tag, tagCode) && !menuItem.HasDropDownItems)
                    return menuItem;

                if (menuItem.HasDropDownItems)
                {
                    var nested = FindItemByTag(menuItem.DropDownItems.Cast<ToolStripItem>(), tagCode);
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        // The header menu builder: an overridden method returning
        // List<ToolStripItem> taking (menu kind enum, context enum, bool). Kind 0
        // is the header menu; contexts 0 and 4 only return the panel toggle item,
        // any other builds the full category menu.
        private static void BuildHeaderMenu(Control browser)
        {
            var builder = browser.GetType().GetMethods(AnyInstance).FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return m.ReturnType == typeof(List<ToolStripItem>)
                    && p.Length == 3
                    && p[0].ParameterType.IsEnum
                    && p[1].ParameterType.IsEnum
                    && p[2].ParameterType == typeof(bool);
            });

            if (builder == null)
            {
                Logger.Info("[ThumbnailBrowser] Header menu builder not found.");
                return;
            }

            var p0 = Enum.ToObject(builder.GetParameters()[0].ParameterType, 0);
            var p1 = Enum.ToObject(builder.GetParameters()[1].ParameterType, 1);
            builder.Invoke(browser, new[] { p0, p1, (object)false });
        }

        // Changing category rebuilds the list on a background thread, which ends
        // with a synchronous Invoke onto the UI thread. So awaiting (not blocking)
        // until that thread has exited means the new list is fully in place.
        private static async Task<bool> WaitForListRebuild(Control browser)
        {
            var list = FindField(browser.GetType(), BrowserListField, AnyInstance)?.GetValue(browser);
            if (list == null) return false;

            var threadField = GetAllFields(list.GetType(), AnyInstance).FirstOrDefault(f => f.FieldType == typeof(Thread));
            if (threadField == null)
            {
                Logger.Info("[ThumbnailBrowser] List rebuild thread field not found.");
                return false;
            }

            int waited = 0;
            while (waited < RebuildTimeoutMs)
            {
                var thread = threadField.GetValue(list) as Thread;
                if (thread == null || !thread.IsAlive) return true;

                await Task.Delay(25);
                waited += 25;
            }

            return false;
        }

        // Step 2: select the entry. First probe silently for an exact match (so a
        // missing value doesn't select the nearest entry instead), then clear and
        // select it again with notify=true - the same path as a user click, which
        // filters the main panel.
        private static bool SelectValue(Control browser, string value)
        {
            var select = browser.GetType().GetMethod(SelectValueMethod, AnyInstance, null, new[] { typeof(string), typeof(bool) }, null);
            var selected = browser.GetType().GetMethod(SelectedValueMethod, AnyInstance, null, Type.EmptyTypes, null);
            if (select == null || selected == null || selected.ReturnType != typeof(string))
            {
                Logger.Info("[ThumbnailBrowser] Select/selected-value methods not found.");
                return false;
            }

            string previous = selected.Invoke(browser, null) as string;

            select.Invoke(browser, new object[] { value, false });
            if (selected.Invoke(browser, null) == null)
            {
                // no entry for this value: restore the previous selection
                select.Invoke(browser, new object[] { previous, false });
                return false;
            }

            select.Invoke(browser, new object[] { null, false });
            select.Invoke(browser, new object[] { value, true });

            ScrollSelectionIntoView(browser);
            return true;
        }

        // Album entries aren't keyed by name: each one is "<sort album>  \0<album
        // unique id>", and the list keeps an id -> representative track map. So
        // albums with the same name by different album artists are distinct
        // entries, and select(value) - a name search - can't find them. Instead,
        // find the entry index ourselves (by the now-playing track's album id,
        // else by comparing album + album artist of each entry's track) and
        // select it through the list's own select(index, userInitiated, notify),
        // which raises the same event as a click.
        private static bool SelectAlbum(Control browser, string album, string albumArtist, string albumId)
        {
            var list = FindField(browser.GetType(), BrowserListField, AnyInstance)?.GetValue(browser);
            if (list == null) return false;

            var items = FindField(list.GetType(), ListItemsField, AnyInstance)?.GetValue(list) as Array;
            var select = FindMethod(list.GetType(), ListSelectIndexMethod, typeof(int), typeof(bool), typeof(bool));
            if (items == null || select == null)
            {
                Logger.Info("[ThumbnailBrowser] Album list items / select-by-index not found.");
                return false;
            }

            var keyField = FindField(items.GetType().GetElementType(), ListItemKeyField, AnyInstance);
            if (keyField == null || keyField.FieldType != typeof(string))
            {
                Logger.Info("[ThumbnailBrowser] Album list item key field not found.");
                return false;
            }

            string[] ids = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                string key = keyField.GetValue(items.GetValue(i)) as string;
                int sep = key?.LastIndexOf('\0') ?? -1;
                ids[i] = sep >= 0 ? key.Substring(sep + 1) : null;
            }

            int index = -1;

            if (!string.IsNullOrEmpty(albumId))
                index = Array.FindIndex(ids, id => id == albumId);

            if (index < 0)
                index = FindAlbumByTags(list, ids, album, albumArtist);

            if (index < 0)
                return false;

            select.Invoke(list, new object[] { index, false, true });
            ScrollSelectionIntoView(browser);
            return true;
        }

        private static int FindAlbumByTags(object list, string[] ids, string album, string albumArtist)
        {
            if (!(FindField(list.GetType(), ListAlbumTracksField, AnyInstance)?.GetValue(list) is System.Collections.IDictionary tracks))
            {
                Logger.Info("[ThumbnailBrowser] Album id -> track map not found.");
                return -1;
            }

            object albumTag = Enum.Parse(_tagCodeType, "Album");
            object albumArtistTag = Enum.Parse(_tagCodeType, "AlbumArtist");
            MethodInfo getTag = null;

            for (int i = 0; i < ids.Length; i++)
            {
                object track = ids[i] == null ? null : tracks[ids[i]];
                if (track == null) continue;

                // every entry's track is the same type: look the method up once
                if (getTag == null)
                {
                    getTag = FindMethod(track.GetType(), TrackGetTagMethod, _tagCodeType);
                    if (getTag == null || getTag.ReturnType != typeof(string))
                    {
                        Logger.Info("[ThumbnailBrowser] Track get-tag method not found.");
                        return -1;
                    }
                }

                if (string.Equals(getTag.Invoke(track, new[] { albumTag }) as string, album, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(albumArtist)
                        || string.Equals(getTag.Invoke(track, new[] { albumArtistTag }) as string, albumArtist, StringComparison.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }

            return -1;
        }

        // The list scrolls by row, and rows hold several thumbnails each.
        private static void ScrollSelectionIntoView(Control browser)
        {
            try
            {
                var list = FindField(browser.GetType(), BrowserListField, AnyInstance)?.GetValue(browser);
                if (list == null) return;

                var listType = list.GetType();
                var indexMethod = listType.GetMethod(ListSelectedIndexMethod, AnyInstance, null, Type.EmptyTypes, null);
                var ensureVisible = listType.GetMethod(ListEnsureRowVisibleMethod, AnyInstance, null, new[] { typeof(int) }, null);
                var perRowField = FindField(listType, ListItemsPerRowField, AnyInstance);

                if (indexMethod?.ReturnType != typeof(int) || ensureVisible == null || perRowField?.FieldType != typeof(int))
                {
                    Logger.Info("[ThumbnailBrowser] Scroll members not found; skipping scroll.");
                    return;
                }

                int index = (int)indexMethod.Invoke(list, null);
                int perRow = Math.Max(1, (int)perRowField.GetValue(list));
                if (index >= 0)
                    ensureVisible.Invoke(list, new object[] { index / perRow });
            }
            catch (Exception ex)
            {
                Logger.Error("Error scrolling Thumbnail Browser", ex);
            }
        }
    }
}
