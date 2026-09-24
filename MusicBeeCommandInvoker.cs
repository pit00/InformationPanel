using System;
using System.Collections.Generic;
using System.Reflection;

namespace MusicBeePlugin
{
    // Invokes MusicBee's internal commands (by name, matching MusicBee's own
    // ApplicationCommand enum member names) via reflection, the same way they are
    // dispatched when a hotkey is pressed. MusicBee's public API only exposes a
    // fixed set of actions, so this locates the internal command dispatcher - a
    // static method taking MusicBee's own ApplicationCommand enum, an object
    // parameter, and an IList<Track> parameter (found by decompiling MusicBee.exe:
    // commands like ViewShowFullSizeArtwork read tracks[0] from that third
    // parameter, which is why simply passing null for it did nothing) - and
    // invokes it directly, optionally with a track resolved from a file path.
    internal static class MusicBeeCommandInvoker
    {
        private static MethodInfo _invokeCommandMethod;
        private static Type _applicationCommandType;
        private static Type _trackType;
        private static MethodInfo _trackFromPathMethod;
        private static bool _tried;

        public static bool Invoke(string commandName) => InvokeCore(commandName, null);

        // Resolves a MusicBee internal Track object for the given file path (via
        // reflection - decompiling showed this is a simple static factory method
        // taking a string and returning the internal Track type) and passes it as
        // a single-item track list, matching what commands like
        // ViewShowFullSizeArtwork expect.
        public static bool InvokeWithTrack(string commandName, string filePath)
        {
            EnsureLoaded();

            if (_trackFromPathMethod == null)
                return InvokeCore(commandName, null);

            try
            {
                object track = _trackFromPathMethod.Invoke(null, new object[] { filePath });
                if (track == null)
                    return InvokeCore(commandName, null);

                var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_trackType));
                list.Add(track);
                return InvokeCore(commandName, list);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resolving track for command '{commandName}'", ex);
                return InvokeCore(commandName, null);
            }
        }

        private static bool InvokeCore(string commandName, object trackList)
        {
            EnsureLoaded();

            if (_invokeCommandMethod == null)
                return false;

            try
            {
                object commandValue = Enum.Parse(_applicationCommandType, commandName);
                _invokeCommandMethod.Invoke(null, new object[] { commandValue, null, trackList });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error invoking command '{commandName}'", ex);
                return false;
            }
        }

        private static void EnsureLoaded()
        {
            if (_tried) return;
            _tried = true;

            try
            {
                var mbAssembly = ReflectionHelper.MusicBeeAssembly;
                if (mbAssembly == null)
                {
                    Logger.Info("MusicBeeCommandInvoker: MusicBee assembly not found.");
                    return;
                }

                // Single pass over MusicBee's (many) types: the dispatcher tells us
                // the Track type, which the track-from-path factory returns.
                var candidates = new List<MethodInfo>();
                foreach (var type in mbAssembly.GetTypes())
                {
                    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var p = m.GetParameters();
                        if (_invokeCommandMethod == null && IsDispatcher(p))
                        {
                            _invokeCommandMethod = m;
                            _applicationCommandType = p[0].ParameterType;
                            _trackType = p[2].ParameterType.GetGenericArguments()[0];
                        }
                        else if (p.Length == 1 && p[0].ParameterType == typeof(string) && m.ReturnType.Assembly == mbAssembly)
                        {
                            candidates.Add(m);
                        }
                    }
                }

                if (_invokeCommandMethod == null)
                {
                    Logger.Info("MusicBeeCommandInvoker: no matching command dispatcher found.");
                    return;
                }

                _trackFromPathMethod = candidates.Find(m => m.ReturnType == _trackType);
            }
            catch (Exception ex)
            {
                Logger.Error("Error resolving MusicBee command dispatcher", ex);
            }
        }

        private static bool IsDispatcher(ParameterInfo[] p)
        {
            return p.Length == 3
                && p[0].ParameterType.IsEnum
                && p[0].ParameterType.Name == "ApplicationCommand"
                && p[1].ParameterType == typeof(object)
                && p[2].ParameterType.IsGenericType
                && p[2].ParameterType.GetGenericTypeDefinition() == typeof(IList<>);
        }
    }
}
