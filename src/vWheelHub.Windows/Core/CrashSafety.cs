using System;
using System.Collections.Generic;

namespace SimRacingHub.Core
{
    public static class CrashSafety
    {
        private static readonly object _lock = new object();
        private static readonly List<KeyValuePair<string, Action>> _actions = new List<KeyValuePair<string, Action>>();

        public static void Register(string name, Action cleanup)
        {
            if (string.IsNullOrEmpty(name) || cleanup == null) return;

            lock (_lock)
            {
                var entry = new KeyValuePair<string, Action>(name, cleanup);
                int index = IndexOf(name);
                if (index >= 0) _actions[index] = entry;
                else _actions.Add(entry);
            }
        }

        public static void Unregister(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            lock (_lock)
            {
                int index = IndexOf(name);
                if (index >= 0) _actions.RemoveAt(index);
            }
        }

        public static void RunAll()
        {
            List<KeyValuePair<string, Action>> pending;

            lock (_lock)
            {
                if (_actions.Count == 0) return;
                pending = new List<KeyValuePair<string, Action>>(_actions);
                _actions.Clear();
            }

            foreach (var entry in pending)
            {
                try
                {
                    entry.Value();
                    AppLogger.Instance.LogInfo($"Emergency cleanup '{entry.Key}' completed", showInStatusBar: false);
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Emergency cleanup '{entry.Key}' failed", ex);
                }
            }
        }

        public static string[] PendingNames()
        {
            lock (_lock)
            {
                var names = new string[_actions.Count];
                for (int i = 0; i < _actions.Count; i++) names[i] = _actions[i].Key;
                return names;
            }
        }

        private static int IndexOf(string name)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (string.Equals(_actions[i].Key, name, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }
    }
}
