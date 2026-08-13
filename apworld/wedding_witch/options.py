from dataclasses import dataclass

from Options import (DefaultOnToggle, PerGameCommonOptions, Range,
                     StartInventoryPool)

from .constants import (FORMS, SHOP_LOCATION_MAX, SHOP_LOCATION_MIN,
                        TOTAL_UPGRADE_LEVELS)


class GoalForms(Range):
    """How many different witch forms must clear the Nightmare Wedding boss.

    Each one is a separate winning run, since the form you finish in is decided by
    which potion type you level. At 6 you have to win as Big Breasts, Small Breasts,
    Corruption, Beast, Muscle and Hips; at 1 any single form ends the seed.

    Winning as the untransformed witch never counts."""
    display_name = "Goal Forms"
    range_start = 1
    range_end = len(FORMS)
    default = len(FORMS)


class ShopLocations(Range):
    """How many Archipelago checks the witch's shop stocks.

    The mod closes the upgrade shop and puts checks on its shelves instead, because
    Love Coins would otherwise have nothing left to buy. Shelf *n* costs 140 coins times
    *n*, so the default twenty come to 29,400 — about three quarters of what the unmodded
    shop charged to max every upgrade. Raising this appends dearer shelves without
    repricing the ones below.

    Your upgrade levels are on the menu's other button, unbuyable."""
    display_name = "Shop Locations"
    range_start = SHOP_LOCATION_MIN
    range_end = SHOP_LOCATION_MAX
    default = 20


class PotionSlotLocations(DefaultOnToggle):
    """Make each of the three potions offered after a stage its own check, rather
    than one check for the screen however you pick.

    On, a stage pays out three checks across as many visits, for 54 in all. That costs
    almost nothing in practice: collecting a difficulty's six boss forms already means
    six runs of it, and six visits to a stage more than cover its three slots.

    Off there are only 18, which leaves too little outside the shop and forces Shop
    Locations up to carry the item pool."""
    display_name = "Potion Slot Locations"


class HardUpgradesRequired(Range):
    """Upgrade levels the multiworld expects you to hold before Hard Wedding's potion
    and boss locations count as reachable, on top of the Progressive Difficulty that
    opens it at all.

    The shop no longer sells upgrades, so the only power you have is what the multiworld
    hands you — this is what stops a seed putting the run that finishes it behind a
    difficulty you have nothing to bring to."""
    display_name = "Hard Upgrade Levels Required"
    range_start = 0
    range_end = TOTAL_UPGRADE_LEVELS
    default = 15


class NightmareUpgradesRequired(Range):
    """Upgrade levels the multiworld expects you to hold before Nightmare Wedding's
    potion and boss locations count as reachable, on top of both Progressive
    Difficulties.

    This is also the finish line: your slot counts as beatable once you hold this
    many and can reach Nightmare, since its boss is what the goal asks for. Raised
    to at least the Hard requirement if you set it lower."""
    display_name = "Nightmare Upgrade Levels Required"
    range_start = 0
    range_end = TOTAL_UPGRADE_LEVELS
    default = 35


@dataclass
class WeddingWitchOptions(PerGameCommonOptions):
    start_inventory_from_pool: StartInventoryPool
    goal_forms: GoalForms
    shop_locations: ShopLocations
    potion_slot_locations: PotionSlotLocations
    hard_upgrades_required: HardUpgradesRequired
    nightmare_upgrades_required: NightmareUpgradesRequired
