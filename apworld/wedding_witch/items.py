"""Item table: every shop upgrade level, and the difficulty ladder."""
from typing import Dict, List

from BaseClasses import ItemClassification

from .constants import (FILLER_ITEMS, GOLD_ITEM, ITEM_BASE_ID,
                        PROGRESSIVE_DIFFICULTY, PROGRESSIVE_DIFFICULTY_COUNT,
                        UPGRADES, UPGRADE_ITEM_GROUP)

PROGRESSIVE_DIFFICULTY_ID = ITEM_BASE_ID + 800
GOLD_ITEM_ID = ITEM_BASE_ID + 850
FILLER_ID_BASE = ITEM_BASE_ID + 900

# One item per upgrade; a copy raises its level.
UPGRADE_ITEMS: Dict[str, int] = {
    display: ITEM_BASE_ID + index
    for index, (_, display, _) in enumerate(UPGRADES)
}

FILLER_ITEM_IDS: Dict[str, int] = {
    name: FILLER_ID_BASE + index for index, name in enumerate(FILLER_ITEMS)
}

ITEM_NAME_TO_ID: Dict[str, int] = dict(UPGRADE_ITEMS)
ITEM_NAME_TO_ID[PROGRESSIVE_DIFFICULTY] = PROGRESSIVE_DIFFICULTY_ID
ITEM_NAME_TO_ID[GOLD_ITEM] = GOLD_ITEM_ID
ITEM_NAME_TO_ID.update(FILLER_ITEM_IDS)

ITEM_NAME_GROUPS: Dict[str, set] = {
    UPGRADE_ITEM_GROUP: set(UPGRADE_ITEMS),
    "Gifts": set(FILLER_ITEMS),
}

# How many copies of each item the pool holds — one upgrade item per purchasable level,
# one difficulty item per difficulty past the first.
ITEM_COUNTS: Dict[str, int] = {
    display: max_level for _, display, max_level in UPGRADES
}
ITEM_COUNTS[PROGRESSIVE_DIFFICULTY] = PROGRESSIVE_DIFFICULTY_COUNT


def classification(name: str) -> ItemClassification:
    """Upgrades are progression: the difficulty gates count them by group."""
    if name in FILLER_ITEM_IDS or name == GOLD_ITEM:
        return ItemClassification.filler
    return ItemClassification.progression


def item_pool_names() -> List[str]:
    return [name
            for name, count in ITEM_COUNTS.items()
            for _ in range(count)]
