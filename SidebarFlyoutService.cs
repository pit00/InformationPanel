using System;
using System.Reflection;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    // MusicBee's FileNewTab command (decompiled from MusicBee.exe v3.6): when the
    // current layout has no Navigator panel docked (i.e. the left sidebar is
    // hidden), it pops the left sidebar out as a temporary flyout so the user can
    // pick what to open in the empty tab. We fill the tab straight away, so that
    // flyout just gets in the way. This closes it the same way pressing Escape
    // does (the flyout's own close method, which puts the docked panels back).
    internal static class SidebarFlyoutService
    {
        // Main form: static field holding the open sidebar flyout, or null.
        private const string FlyoutField = "#=z4_TemWWQ5Lql";
        // Flyout: close method called by MusicBee's Escape handler.
        private const string FlyoutCloseMethod = "#=zy9Ubvnk=";

        public static bool IsOpen() => GetFlyout() != null;

        public static void Close()
        {
            try
            {
                var flyout = GetFlyout();
                if (flyout == null) return;

                var close = flyout.GetType().GetMethod(FlyoutCloseMethod, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (close != null)
                    close.Invoke(flyout, null);
                else
                    Logger.Info($"[SidebarFlyout] Close method {FlyoutCloseMethod} not found on {flyout.GetType().FullName}; leaving the flyout open.");
            }
            catch (Exception ex)
            {
                Logger.Error("Error closing sidebar flyout", ex);
            }
        }

        private static Form GetFlyout()
        {
            var mainFormType = ReflectionHelper.MainWindow?.GetType();
            if (mainFormType == null) return null;

            var field = ReflectionHelper.FindField(mainFormType, FlyoutField, ReflectionHelper.AnyStatic);
            return field?.GetValue(null) as Form;
        }
    }
}
