using System.Collections.Generic;
using Archipelago.MultiClient.Net.Enums;

namespace WeddingWitchArchipelago.Archipelago;

/// What is actually sitting in each potion slot, so an unclaimed one can say
/// "Progressive Sword — for Alice" rather than "Archipelago Item".
///
/// Filled by one batch scout at connect rather than on demand: the potion screen is
/// built synchronously in PotionCanvas.OnEnable, and an async lookup would land
/// several frames after it had already drawn itself.
public static class ScoutCache
{
    public readonly struct Entry
    {
        public readonly string ItemName;
        public readonly string Receiver;
        public readonly ItemFlags Flags;

        public Entry(string itemName, string receiver, ItemFlags flags)
        {
            ItemName = itemName;
            Receiver = receiver;
            Flags = flags;
        }

        /// The classification, in the words the Archipelago client uses for it.
        public string Kind
        {
            get
            {
                if ((Flags & ItemFlags.Advancement) != 0) return "Progression";
                if ((Flags & ItemFlags.Trap) != 0) return "Trap";
                if ((Flags & ItemFlags.NeverExclude) != 0) return "Useful";
                return "Filler";
            }
        }
    }

    private static readonly Dictionary<string, Entry> ByLocation = new Dictionary<string, Entry>();

    public static int Count { get { lock (ByLocation) return ByLocation.Count; } }

    public static void Clear()
    {
        lock (ByLocation) ByLocation.Clear();
    }

    /// Written from the scout's continuation, read while the potion screen builds.
    public static void Add(string location, string itemName, string receiver, ItemFlags flags)
    {
        lock (ByLocation) ByLocation[location] = new Entry(itemName, receiver, flags);
    }

    public static bool TryGet(string location, out Entry entry)
    {
        lock (ByLocation) return ByLocation.TryGetValue(location, out entry);
    }
}
