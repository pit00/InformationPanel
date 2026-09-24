using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    // MusicBee's own artwork display has a right-click context menu, but there's
    // no documented API to invoke it, and its concrete type is obfuscated (can't
    // find it by name). ContextMenuStrip/ContextMenu, however, are ordinary public
    // members inherited from System.Windows.Forms.Control - not MusicBee-specific -
    // so if the control that owns that menu can be found by shape (roughly square,
    // a locally-assigned menu rather than one merely inherited from an ancestor),
    // the exact same menu instance can be shown from here.
    internal static class NativeArtworkMenuService
    {
        private static ContextMenuStrip _foundStrip;
        private static ContextMenu _foundMenu;
        private static bool _searched;

        public static bool TryShow(Control invoker, Point screenPosition)
        {
            EnsureFound();

            if (_foundStrip != null)
            {
                _foundStrip.Show(screenPosition);
                return true;
            }

            if (_foundMenu != null)
            {
                _foundMenu.Show(invoker, invoker.PointToClient(screenPosition));
                return true;
            }

            return false;
        }

        private static void EnsureFound()
        {
            if (_searched) return;
            _searched = true;

            try
            {
                var root = ReflectionHelper.MainWindow;
                if (root == null) return;

                var stripCandidates = new List<Control>();
                var menuCandidates = new List<Control>();

                Walk(root, null, null, stripCandidates, menuCandidates);

                _foundStrip = PickSquarest(stripCandidates)?.ContextMenuStrip;
                if (_foundStrip == null)
                    _foundMenu = PickSquarest(menuCandidates)?.ContextMenu;
            }
            catch (Exception ex)
            {
                Logger.Error("Error locating native artwork context menu", ex);
            }
        }

        // Walks the tree tracking the ContextMenuStrip/ContextMenu inherited from
        // ancestors, so only controls that assign their OWN (different) menu are
        // flagged - a plain inherited menu from some root-level assignment would
        // otherwise show up on nearly every control and drown out the real owner.
        private static void Walk(Control control, ContextMenuStrip inheritedStrip, ContextMenu inheritedMenu,
            List<Control> stripCandidates, List<Control> menuCandidates)
        {
            if (control.ContextMenuStrip != null && control.ContextMenuStrip != inheritedStrip)
                stripCandidates.Add(control);

            if (control.ContextMenu != null && control.ContextMenu != inheritedMenu)
                menuCandidates.Add(control);

            var nextStrip = control.ContextMenuStrip ?? inheritedStrip;
            var nextMenu = control.ContextMenu ?? inheritedMenu;

            foreach (Control child in control.Controls)
                Walk(child, nextStrip, nextMenu, stripCandidates, menuCandidates);
        }

        private static Control PickSquarest(List<Control> candidates)
        {
            Control best = null;
            foreach (var c in candidates)
            {
                if (c.Width < 60 || c.Height < 60) continue;

                double ratio = (double)c.Width / c.Height;
                if (ratio < 0.7 || ratio > 1.4) continue;

                if (best == null || (c.Width * c.Height) < (best.Width * best.Height))
                    best = c;
            }
            return best;
        }
    }
}
