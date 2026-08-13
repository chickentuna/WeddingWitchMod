"""Location table.

Three sources: the potion offered at the end of a stage, clearing the difficulty's boss —
six times over on Nightmare, once anywhere else — and the witch's shop — which the mod turns into a stall
selling Archipelago checks for Love Coins, since with the upgrades gone there is nothing
else left to spend them on.

Clearing a stage is not itself a check. The potion screen it leads to is, so a cleared
stage already pays out once and counting the clear as well would double every run.

`location_name_to_id` holds every name any seed could ask for; which of them a
given seed actually creates comes from its options.
"""
from typing import Dict, List, Optional, Tuple

from .constants import (BOSS_BASE, DIFFICULTIES, FORMS, PER_FORM_BOSS, POTION_BASE,
                        POTION_SLOTS, SHOP_BASE, SHOP_LOCATION_MAX)

# Room for the longest difficulty and then some, so adding Hell later doesn't
# renumber anything.
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
    """Any form will do. This is the shape on every difficulty but the last."""
    return f"{difficulty} - Boss Cleared"


def boss_form_location(difficulty: str, form: str) -> str:
    """Split six ways, for the difficulty where transforming is reliably possible."""
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

        # Both shapes are registered for every difficulty, so which one a seed uses can
        # change without renumbering anything. Form values start at 1, leaving 0 free.
        ids[boss_location(display)] = BOSS_BASE + enum_value * BOSS_SPAN
        for form_display, form_value in FORMS:
            ids[boss_form_location(display, form_display)] = \
                BOSS_BASE + enum_value * BOSS_SPAN + form_value

    for slot in range(1, SHOP_LOCATION_MAX + 1):
        ids[shop_location(slot)] = SHOP_BASE + slot

    return ids


LOCATION_NAME_TO_ID = _build_ids()


def locations_by_region(potion_slots: bool, shop_slots: int) -> Dict[str, List[str]]:
    """The subset a seed creates, grouped by the region that gates it.

    Shop stock is its own region with no requirement: Love Coins drop on any
    difficulty, so grinding Normal can pay for all of it.
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
