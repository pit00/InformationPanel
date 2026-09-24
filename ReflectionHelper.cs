using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin
{
    // Shared lookups for the services that reach into MusicBee's internals.
    internal static class ReflectionHelper
    {
        public const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static Control MainWindow => Control.FromHandle(mbApi.MB_GetWindowHandle());

        // MusicBee.exe: defines the main window as well as internal types like
        // MusicBee.Media.TagCode and ApplicationCommand.
        public static Assembly MusicBeeAssembly => MainWindow?.GetType().Assembly;

        public static FieldInfo FindField(Type type, string name, BindingFlags flags)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        public static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var method = t.GetMethod(name, AnyInstance | BindingFlags.DeclaredOnly, null, parameterTypes, null);
                if (method != null) return method;
            }
            return null;
        }

        // Fields declared by MusicBee's own classes, i.e. excluding those
        // inherited from WinForms' Control and its ancestors.
        public static IEnumerable<FieldInfo> GetAllFields(Type type, BindingFlags flags)
        {
            for (var t = type; t != null && t != typeof(Control); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags | BindingFlags.DeclaredOnly))
                    yield return field;
            }
        }
    }
}
