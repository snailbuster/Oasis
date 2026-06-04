using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    public static class BattleLog
    {
        public static event Action<string> OnLog;
        public static List<string> History = new List<string>();
        public const int MaxLines = 80;

        public static void Log(string msg)
        {
            Debug.Log("[Battle] " + msg);
            History.Add(msg);
            if (History.Count > MaxLines) History.RemoveAt(0);
            OnLog?.Invoke(msg);
        }

        public static void Clear()
        {
            History.Clear();
        }
    }
}
