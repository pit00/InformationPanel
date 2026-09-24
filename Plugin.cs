using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        public static MusicBeeApiInterface mbApi;

        private readonly PluginInfo about = new PluginInfo();
        private Information panel;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApi = new MusicBeeApiInterface();
            mbApi.Initialise(apiInterfacePtr);

            var storagePath = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "Information");
            Logger.Initialize(Path.Combine(storagePath, "error.log"));
            PanelSettings.Load();

            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Information";
            about.Description = "A Track & Artist panel where individual artists and genres can be clicked. Add it via Preferences > Layout > Arrange Panels";
            about.Author = "starkgate";
            // Per the official SDK sample: setting this gives the dockable panel a
            // MusicBee-drawn header with this text, which is what makes the header
            // clickable at all - GetMenuItems() has nothing to be clicked without one.
            about.TargetApplication = "Information";
            about.Type = PluginType.PanelView;
            about.VersionMajor = 1;
            about.VersionMinor = 0;
            about.Revision = 0;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents;
            about.ConfigurationPanelHeight = 0;

            return about;
        }

        // Not part of the public interop struct - MusicBee detects and calls this via
        // reflection, only for panels the user has enabled in Preferences > Layout >
        // Arrange Panels, passing the container it created for us to fill. The return
        // value is a size hint: negative = fill available space, 0 = user-resizable,
        // positive = fixed height in pixels (DPI-scaled).
        public int OnDockablePanelCreated(Control container)
        {
            // MusicBee appears to call this via reflection from a thread other than
            // the one that owns `container`; creating/parenting controls directly
            // here throws "Controls created on one thread cannot be parented to a
            // control on a different thread". Marshal onto container's own thread.
            if (container.InvokeRequired)
                return (int)container.Invoke(new Func<Control, int>(OnDockablePanelCreated), container);

            try
            {
                panel = new Information(container);
                container.Controls.Add(panel);
                panel.RefreshTrackInfo();
            }
            catch (Exception ex)
            {
                Logger.Error("OnDockablePanelCreated() threw", ex);
            }
            return 0; // user-resizable
        }

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            try
            {
                switch (type)
                {
                    // only affect the panel when it shows the playing track
                    case NotificationType.TrackChanging:
                    case NotificationType.TrackChanged:
                        if (PanelSettings.TrackSource == TrackSource.PlayingTrack)
                            panel?.RequestRefresh();
                        break;

                    // sent once artwork loaded asynchronously (e.g. downloaded) is available
                    case NotificationType.NowPlayingArtworkReady:
                        if (PanelSettings.TrackSource == TrackSource.PlayingTrack)
                            panel?.RequestRefresh(reloadArtwork: true);
                        break;

                    case NotificationType.TagsChanged:
                    case NotificationType.RatingChanged:
                        panel?.OnFileChanged(sourceFileUrl);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling notification {type}", ex);
            }
        }

        // Not in the SDK: MusicBee doesn't send SelectedFileChanged through
        // ReceiveNotification, but instead reflects for a plugin method literally
        // named "OnSelectedFilesChanged" and calls it with the selected files'
        // paths (found by decompiling MusicBee.exe v3.6).
        public void OnSelectedFilesChanged(string[] filenames)
        {
            try
            {
                panel?.OnSelectedFilesChanged(filenames);
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling selected files change", ex);
            }
        }

        public bool Configure(IntPtr panelHandle)
        {
            var mainWindow = Control.FromHandle(mbApi.MB_GetWindowHandle());
            if (mainWindow != null && mainWindow.InvokeRequired)
                return (bool)mainWindow.Invoke(new Func<IntPtr, bool>(Configure), panelHandle);

            try
            {
                using (var form = new ConfigForm())
                {
                    if (form.ShowDialog(mainWindow) == DialogResult.OK)
                        panel?.RefreshTrackInfo();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening Information settings", ex);
                return false;
            }
        }

        // The official SDK sample calls this GetHeaderMenuItems(), but decompiling
        // MusicBee.exe shows it actually reflects for a method literally named
        // "GetMenuItems" (case-sensitive, ordinal) taking no parameters and
        // returning List<ToolStripItem> - invoked from the same generic "show this
        // panel's menu" code MusicBee uses for both the header right-click and
        // (it being the only call site for this in the whole binary) very likely
        // the Arrange Panels "..." button too.
        public List<ToolStripItem> GetMenuItems()
        {
            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += (s, e) => Configure(IntPtr.Zero);

            // Same choice the native Track Information panel offers in its header menu.
            return new List<ToolStripItem>
            {
                CreateTrackSourceItem("Playing Track", TrackSource.PlayingTrack),
                CreateTrackSourceItem("Selected Track", TrackSource.SelectedTrack),
                new ToolStripSeparator(),
                settingsItem,
            };
        }

        private ToolStripMenuItem CreateTrackSourceItem(string text, TrackSource source)
        {
            var item = new ToolStripMenuItem(text) { Checked = PanelSettings.TrackSource == source };
            item.Click += (s, e) =>
            {
                if (PanelSettings.TrackSource == source) return;
                PanelSettings.TrackSource = source;
                PanelSettings.Save();
                panel?.RefreshTrackInfo();
            };
            return item;
        }

        public void Close(PluginCloseReason reason)
        {
            panel = null;
        }
    }
}
