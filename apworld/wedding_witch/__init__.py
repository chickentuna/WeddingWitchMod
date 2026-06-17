"""
Archipelago world for Wedding Witch (STUB — upgrades only).

This MUST stay in sync with the client mod's id scheme in
  src/Game/UpgradeCatalog.cs
i.e. the same BASE, SPAN, ItemBase, the same slug ORDER, and the same per-upgrade MaxLevel.

Model: each meta-shop upgrade is leveled 0..MaxLevel. Each level is one AP *location*
("<name> Lv N", checked when the player buys it in game). Each AP *item* is a progressive
"+1 level" for that upgrade (MaxLevel copies). Buying a level sends the check; receiving the
progressive item is what actually raises the persisted level in game.

Install: copy/symlink this `wedding_witch` folder into your Archipelago `worlds/` dir, or zip it
to `wedding_witch.apworld` and drop it in `custom_worlds/`. Then it appears as game "Wedding Witch".

TODO before real use:
  * Replace MAX_LEVEL placeholders with the real maxLevels captured in game (Plugin "Dump Upgrades").
  * Add real logic/goal rules; right now everything is in one region with no access requirements.
"""
from typing import Dict, List, Tuple

from BaseClasses import Item, ItemClassification, Location, Region
from worlds.AutoWorld import World, WebWorld

GAME = "Wedding Witch"
BASE = 8_451_000
SPAN = 32
ITEM_BASE = BASE + 100_000

# (slug, display) — ORDER IS THE ID CONTRACT with UpgradeCatalog.All in the C# mod. Append only.
UPGRADES: List[Tuple[str, str]] = [
    ("health",        "Witch's Hat"),
    ("attack",        "Witch's Orb"),
    ("attackSpeed",   "Witch's Gloves"),
    ("radius",        "Witch's Ring"),
    ("lifespan",      "Witch's Watch"),
    ("castAmount",    "Phantasm Mirror"),
    ("moveSpeed",     "Witch's Shoes"),
    ("hitCount",      "Witch's Necklace"),
    ("critRate",      "Witch's Earrings"),
    ("critDamage",    "Magical Optics"),
    ("dodge",         "Cape of Protection"),
    ("regen",         "Witch's Bracelet"),
    ("exp",           "Book of Hitomi"),
    ("gold",          "Maneki Neko"),
    ("levelUpOption", "Witch's Bag"),
    ("rewardCount",   "Eye Of The Beholder"),
    ("absorbRadius",  "Witch's Doll"),
]

# TODO: real maxLevels (== UpgradeData.cost.Length) from the in-game "Dump Upgrades" capture.
MAX_LEVEL: Dict[str, int] = {slug: 5 for slug, _ in UPGRADES}

DISPLAY = {slug: name for slug, name in UPGRADES}
INDEX = {slug: i for i, (slug, _) in enumerate(UPGRADES)}


def _location_table() -> Dict[str, int]:
    t = {}
    for slug, _ in UPGRADES:
        for lvl in range(1, MAX_LEVEL[slug] + 1):
            t[f"{DISPLAY[slug]} Lv{lvl}"] = BASE + INDEX[slug] * SPAN + lvl
    return t


def _item_table() -> Dict[str, int]:
    return {f"Progressive {DISPLAY[slug]}": ITEM_BASE + INDEX[slug] for slug, _ in UPGRADES}


LOCATION_NAME_TO_ID = _location_table()
ITEM_NAME_TO_ID = _item_table()


class WeddingWitchItem(Item):
    game = GAME


class WeddingWitchLocation(Location):
    game = GAME


class WeddingWitchWeb(WebWorld):
    theme = "dirt"


class WeddingWitchWorld(World):
    """Wedding Witch — meta-shop upgrade randomizer (stub)."""
    game = GAME
    web = WeddingWitchWeb()
    item_name_to_id = ITEM_NAME_TO_ID
    location_name_to_id = LOCATION_NAME_TO_ID

    def create_item(self, name: str) -> WeddingWitchItem:
        return WeddingWitchItem(name, ItemClassification.useful, ITEM_NAME_TO_ID[name], self.player)

    def create_items(self) -> None:
        for slug, _ in UPGRADES:
            name = f"Progressive {DISPLAY[slug]}"
            for _ in range(MAX_LEVEL[slug]):  # one progressive copy per purchasable level
                self.multiworld.itempool.append(self.create_item(name))

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        shop = Region("Upgrade Shop", self.player, self.multiworld)
        for loc_name, loc_id in LOCATION_NAME_TO_ID.items():
            shop.locations.append(WeddingWitchLocation(self.player, loc_name, loc_id, shop))
        menu.connect(shop)
        self.multiworld.regions += [menu, shop]

    def set_rules(self) -> None:
        # TODO real logic. For now everything is reachable; goal = collect every progressive upgrade.
        self.multiworld.completion_condition[self.player] = lambda state: all(
            state.has(f"Progressive {DISPLAY[slug]}", self.player, MAX_LEVEL[slug])
            for slug, _ in UPGRADES
        )
