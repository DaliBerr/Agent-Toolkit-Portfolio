using System;
using UnityEngine;

namespace Vocalith.RuntimeAutomation.GameplayMcp
{
    public static class RuntimeAutomationMcpBridge
    {
        private static readonly object Gate = new();
        private static RuntimeAutomationHost current;

        internal static RuntimeAutomationHost Current
        {
            get
            {
                lock (Gate) return current;
            }
        }

        public static void Install(RuntimeAutomationHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            lock (Gate)
            {
                if (current != null && !ReferenceEquals(current, host))
                {
                    throw new InvalidOperationException("A runtime automation host is already installed.");
                }

                current = host;
            }
        }

        public static void Clear(RuntimeAutomationHost host = null)
        {
            lock (Gate)
            {
                if (host == null || ReferenceEquals(current, host)) current = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }
    }
}
