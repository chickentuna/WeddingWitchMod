"""Values shared with the mod; the C# side of them is in UpgradeCatalog.cs and
Locations.cs.
"""

GAME_NAME = "Wedding Witch"

BASE_ID = 8_451_000
ITEM_BASE_ID = BASE_ID + 100_000

BOSS_BASE = BASE_ID + 500
POTION_BASE = BASE_ID + 1_000
SHOP_BASE = BASE_ID + 2_000

# Slots PotionManager.GetRandomEnchantList fills.
POTION_SLOTS = 3

# The ceiling on shop stock.
SHOP_LOCATION_MAX = 60

# The smallest shop_locations the item pool fits in.
SHOP_LOCATION_MIN = 13

# (display, Difficulty enum value, stage count), playable difficulties only. The boss
# sits past the last stage and is not counted.
DIFFICULTIES = (
    ("Normal", 0, 5),
    ("Hard", 1, 6),
    ("Nightmare", 2, 7),
)

GOAL_DIFFICULTY = "Nightmare"

# Difficulties whose boss carries a location per form rather than a single clear.
PER_FORM_BOSS = ("Nightmare",)

# Opens the next difficulty, in place of the vanilla unlock. One copy per difficulty
# past the first.
PROGRESSIVE_DIFFICULTY = "Progressive Difficulty"
PROGRESSIVE_DIFFICULTY_COUNT = len(DIFFICULTIES) - 1

# How many copies each difficulty needs before its locations are reachable.
DIFFICULTY_REQUIREMENT = {display: index for index, (display, _, _) in enumerate(DIFFICULTIES)}

# (display, BodyState enum value). Normal is untransformed and carries no location.
FORMS = (
    ("Big Breasts", 1),
    ("Small Breasts", 2),
    ("Corruption", 3),
    ("Beast", 4),
    ("Muscle", 5),
    ("Hips", 6),
)

# (UpgradeData.loadKey, display, cost.Length). Append only — the index is the id.
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

# Filler. The mod recognises a gift by this prefix — see ApItems.IsFiller.
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
    "Blindfold",
    "Lace Chemisette",
    "Silk Tap Pants",
    "Torsolette",
    "Heeled Boots",
    "Longline Bra",
    "Crystal Earrings",
)

FILLER_ITEMS = tuple(FILLER_PREFIX + gift for gift in FILLER_GIFTS)

# Filler that grants Love Coins, kept outside FILLER_PREFIX so the mod acts on it rather
# than ignoring it. Worth one shelf at ShopPatch.PriceCapSlot.
GOLD_ITEM = "+2800 Coins"
GOLD_PER_ITEM = 2800
