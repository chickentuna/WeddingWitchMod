"""Values shared with the mod. Everything here has a counterpart on the C# side —
see WeddingWitchArchipelago/UpgradeCatalog.cs and Locations.cs — and the two must
not drift.
"""

GAME_NAME = "Wedding Witch"

# AP ids only need to be unique within the game, but they must stay stable across
# releases or existing seeds break.
BASE_ID = 8_451_000
ITEM_BASE_ID = BASE_ID + 100_000

BOSS_BASE = BASE_ID + 500
POTION_BASE = BASE_ID + 1_000
SHOP_BASE = BASE_ID + 2_000

# PotionManager.GetRandomEnchantList fills Mathf.Min(possibleEnchant.Count, 3)
# slots, and the possible list is never short of three.
POTION_SLOTS = 3

# The ceiling on shop stock. Only `shop_locations` of these are used by a seed, but
# the id table has to hold every name a seed could ever ask for.
SHOP_LOCATION_MAX = 60

# Below this the item pool no longer fits: with Potion Slot Locations on there are 54
# potion screens and 8 boss clears against 75 items. Turning that option off leaves far
# fewer and needs a much bigger shop, which create_items refuses outright rather than
# quietly reshaping the seed.
SHOP_LOCATION_MIN = 13

# (display, Difficulty enum value, stage count). RoadMapCanvas.Start sets
# MissionManager.maxMissionCount from the difficulty; the boss sits on a further
# node past the last floor and is not counted here.
#
# Hell and Impossible exist in the Difficulty enum but SelectDifficultyCanvas
# hardcodes `hellMode.interactable = false`, so neither is playable.
DIFFICULTIES = (
    ("Normal", 0, 5),
    ("Hard", 1, 6),
    ("Nightmare", 2, 7),
)

GOAL_DIFFICULTY = "Nightmare"

# Difficulties whose boss is worth splitting six ways. Only the last one: transforming
# needs one potion type levelled to three, and the shorter runs rarely offer enough of a
# kind to get there, so asking for six forms on Normal would be asking for something the
# run does not reliably allow. The others just want the boss dead, in any form.
#
# THIS IS A CONTRACT WITH THE MOD — Locations.PerFormBoss must agree.
PER_FORM_BOSS = ("Nightmare",)

# Vanilla opens Hard once Normal has been cleared and Nightmare once Hard has, both
# read out of GameData.es3 in SelectDifficultyCanvas.OnEnable. The mod replaces that
# with this item, so which difficulties you can play is the multiworld's to decide.
# One copy per difficulty past the first.
PROGRESSIVE_DIFFICULTY = "Progressive Difficulty"
PROGRESSIVE_DIFFICULTY_COUNT = len(DIFFICULTIES) - 1

# How many copies each difficulty needs before its locations are reachable.
DIFFICULTY_REQUIREMENT = {display: index for index, (display, _, _) in enumerate(DIFFICULTIES)}

# (display, BodyState enum value). PotionManager.bodyState holds the form the witch
# is in; Normal is the untransformed state and carries no location.
FORMS = (
    ("Big Breasts", 1),
    ("Small Breasts", 2),
    ("Corruption", 3),
    ("Beast", 4),
    ("Muscle", 5),
    ("Hips", 6),
)

# (load key, display, max level). The load key is the UpgradeData asset name, which
# is what `UpgradeData.loadKey` returns and what every consumer reads out of
# GameData.es3. Max level is `cost.Length`.
#
# ORDER IS PART OF THE ID FORMULA — append only, never reorder.
UPGRADES = (
    ("Upgrade_WitchHat", "Witch's Hat", 5),
    ("Upgrade_Orb", "Witch's Orb", 5),
    ("Upgrade_Glove", "Witch's Gloves", 5),
    ("Upgrade_Ring", "Witch's Ring", 5),
    ("Upgrade_Watch", "Witch's Watch", 5),
    ("Upgrade_Mirror", "Phantasm Mirror", 1),
    ("Upgrade_Shoes", "Witch's Shoes", 3),
    ("Upgrade_Necklace", "Witch's Necklace", 3),
    ("Upgrade_Earring", "Witch's Earrings", 5),
    ("Upgrade_Monocle", "Magical Optics", 5),
    ("Upgrade_Cape", "Cape of Protection", 5),
    ("Upgrade_Bracelet", "Witch's Bracelet", 5),
    ("Upgrade_HitomiBook", "Book of Hitomi", 5),
    ("Upgrade_Maneki", "Maneki Neko", 5),
    ("Upgrade_Bag", "Witch's Bag", 1),
    ("Upgrade_EyeOfBeholder", "Eye Of The Beholder", 5),
    ("Upgrade_WitchDoll", "Witch's Doll", 5),
)

TOTAL_UPGRADE_LEVELS = sum(max_level for _, _, max_level in UPGRADES)

UPGRADE_ITEM_GROUP = "Upgrades"

# Filler. The witch is extorting wedding gifts out of the devils, so that is what a
# location holding nothing of consequence pays out.
#
# The prefix is the contract with the mod, which recognises filler by it rather than by
# carrying this list twice — see ApItems.IsFiller.
FILLER_PREFIX = "Gift: "

FILLER_GIFTS = (
    "Lace Garter",
    "Silk Camisole",
    "Satin Chemise",
    "Babydoll",
    "6-inch Pumps",
    "Sheer Teddy",
    "Whalebone Corset",
    "Velvet Bustier",
    "Lace Bralette",
    "Dog Collar",
    "Balconette Bra",
    "Garter Belt",
    "Seamed Stockings",
    "Platform Heels",
    "Thigh-Highs",
    "Nylon Pantyhose",
    "Crotchless Panties",
    "Fishnet Tights",
    "Stilettos",
    "Handcuffs",
    "Silk Briefs",
    "Lace Boyshorts",
    "French Knickers",
    "Ribbon Thong",
    "Satin Bodysuit",
    "Chiffon Negligee",
    "Feathered Peignoir",
    "Silk Robe",
    "Waist Cincher",
    "Ankle Strap Heels",
    "Bandeau Top",
    "Halter Bra",
    "Blindfold"
    "Lace Chemisette",
    "Silk Tap Pants",
    "Torsolette",
    "Heeled Boots",
    "Longline Bra",
    "Nipple Rings"
)

FILLER_ITEMS = tuple(FILLER_PREFIX + gift for gift in FILLER_GIFTS)

# The one filler that does something: Love Coins, which the hijacked shop spends. Kept
# out of the Gift prefix on purpose — the mod ignores anything wearing that, and this has
# to be acted on.
#
# THIS IS A CONTRACT WITH THE MOD — ApItems.Gold and GOLD_PER_ITEM must agree.
GOLD_ITEM = "+100 Gold"
GOLD_PER_ITEM = 100
