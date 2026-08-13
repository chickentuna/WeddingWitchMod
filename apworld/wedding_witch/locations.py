"""Location table.

Three sources: the potions offered at the end of a stage, boss clears (one per form on
PER_FORM_BOSS difficulties, one otherwise) and the shop shelves. A stage clear is not
itself a location; the potion screen it leads to is.

`location_name_to_id` holds every id the world defines. Which of them a seed creates
is decided by its options.
"""
from typing import Dict, List, Optional, Tuple

from .constants import (BOSS_BASE, DIFFICULTIES, FORMS, PER_FORM_BOSS, POTION_BASE,
                        POTION_SLOTS, SHOP_BASE, SHOP_LOCATION_MAX)

# Ids reserved per difficulty for its boss locations.
BOSS_SPAN = 10
POTION_DIFFICULTY_SPAN = 100
POTION_STAGE_SPAN = 10

SHOP_REGION = "Shop"


def potion_location(difficulty: str, stage: int, slot: Optional[int] = None) -> str:
    """With Potion Slot Locations off there is one check per potion screen however you
    pick, so the name carries no slot number."""
    if slot is None:
        return f"{difficulty} - Stage {stage} Potion"
    return f"{difficulty} - Stage {stage} Potion {slot}"


def boss_location(difficulty: str) -> str:
    """Boss clear in any form, used on the difficulties outside PER_FORM_BOSS."""
    return f"{difficulty} - Boss Cleared"


def boss_form_location(difficulty: str, form: str) -> str:
    """One location per form, used on the difficulties in PER_FORM_BOSS."""
    return f"{difficulty} - Boss Cleared as {form}"


def shop_location(slot: int) -> str:
    return f"Shop Slot {slot}"


def _potion_id(enum_value: int, stage: int, slot: int) -> int:
    """Slot 0 is the single-check-per-screen variant."""
    return (POTION_BASE
            + enum_value * POTION_DIFFICULTY_SPAN
            + stage * POTION_STAGE_SPAN
            + slot)


def _build_ids() -> Dict[str, int]:
    ids: Dict[str, int] = {}

    for display, enum_value, stages in DIFFICULTIES:
        for stage in range(1, stages + 1):
            ids[potion_location(display, stage)] = _potion_id(enum_value, stage, 0)
            for slot in range(1, POTION_SLOTS + 1):
                ids[potion_location(display, stage, slot)] = \
                    _potion_id(enum_value, stage, slot)

        # Both variants get ids on every difficulty. BodyState values start at 1, so
        # offset 0 is free for the any-form clear.
        ids[boss_location(display)] = BOSS_BASE + enum_value * BOSS_SPAN
        for form_display, form_value in FORMS:
            ids[boss_form_location(display, form_display)] = \
                BOSS_BASE + enum_value * BOSS_SPAN + form_value

    for slot in range(1, SHOP_LOCATION_MAX + 1):
        ids[shop_location(slot)] = SHOP_BASE + slot

    return ids


LOCATION_NAME_TO_ID = _build_ids()


def locations_by_region(potion_slots: bool, shop_slots: int) -> Dict[str, List[str]]:
    """The subset a seed creates, grouped by the region that gates it. Shop stock is its
    own region, ungated — Love Coins drop on any difficulty.
    """
    by_region: Dict[str, List[str]] = {display: [] for display, _, _ in DIFFICULTIES}
    by_region[SHOP_REGION] = [shop_location(slot) for slot in range(1, shop_slots + 1)]

    for display, _, stages in DIFFICULTIES:
        region = by_region[display]
        for stage in range(1, stages + 1):
            if potion_slots:
                region.extend(potion_location(display, stage, slot)
                              for slot in range(1, POTION_SLOTS + 1))
            else:
                region.append(potion_location(display, stage))
        if display in PER_FORM_BOSS:
            region.extend(boss_form_location(display, form) for form, _ in FORMS)
        else:
            region.append(boss_location(display))

    return by_region


LOCATION_NAME_GROUPS: Dict[str, set] = {
    "Potions": {name
                for display, _, stages in DIFFICULTIES
                for stage in range(1, stages + 1)
                for name in [potion_location(display, stage)]
                + [potion_location(display, stage, slot)
                   for slot in range(1, POTION_SLOTS + 1)]},
    "Bosses": ({boss_location(display) for display, _, _ in DIFFICULTIES}
               | {boss_form_location(display, form)
                  for display, _, _ in DIFFICULTIES
                  for form, _ in FORMS}),
    "Shop": {shop_location(slot) for slot in range(1, SHOP_LOCATION_MAX + 1)},
}
