from dataclasses import dataclass

from Options import (DefaultOnToggle, PerGameCommonOptions, Range,
                     StartInventoryPool)

from .constants import (FORMS, SHOP_LOCATION_MAX, SHOP_LOCATION_MIN,
                        TOTAL_UPGRADE_LEVELS)


class GoalForms(Range):
    """How many different witch forms must clear the Nightmare Wedding boss, each a
    separate winning run. Untransformed does not count."""
    display_name = "Goal Forms"
    range_start = 1
    range_end = len(FORMS)
    default = len(FORMS)


class ShopLocations(Range):
    """How many locations the witch's shop stocks, in place of the upgrades.

    Shelf *n* costs 4.5 Love Coins times *n*, up to 90 from the twentieth on."""
    display_name = "Shop Locations"
    range_start = SHOP_LOCATION_MIN
    range_end = SHOP_LOCATION_MAX
    default = 20


class PotionSlotLocations(DefaultOnToggle):
    """Make each of the three potions offered after a stage its own location, for 54 in
    all. Off, the screen is one location whichever potion you take, for 18."""
    display_name = "Potion Slot Locations"


class HardUpgradesRequired(Range):
    """Upgrade levels you must hold before Hard Wedding's potion and boss locations
    count as reachable, on top of the Progressive Difficulty that opens it."""
    display_name = "Hard Upgrade Levels Required"
    range_start = 0
    range_end = TOTAL_UPGRADE_LEVELS
    default = 15


class NightmareUpgradesRequired(Range):
    """Upgrade levels you must hold before Nightmare Wedding's potion and boss locations
    count as reachable, on top of both Progressive Difficulties. Raised to match Hard's
    requirement if you set it lower."""
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
