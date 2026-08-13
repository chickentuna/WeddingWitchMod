"""Archipelago world for Wedding Witch.

The mod disables the meta-upgrade shop, making each of its 73 levels an item, and stocks
its shelves with locations instead. Locations come from those shelves, from the potions
offered after a stage, and from boss clears.
"""
import logging
from typing import Any, Dict, List

from BaseClasses import Item, Location, Region, Tutorial
from Options import OptionError
from worlds.AutoWorld import WebWorld, World

from .constants import (DIFFICULTY_REQUIREMENT, FILLER_ITEMS, GAME_NAME,
                        GOAL_DIFFICULTY, GOLD_ITEM, PROGRESSIVE_DIFFICULTY,
                        UPGRADE_ITEM_GROUP)
from .items import (ITEM_NAME_GROUPS, ITEM_NAME_TO_ID, classification,
                    item_pool_names)
from .locations import (LOCATION_NAME_GROUPS, LOCATION_NAME_TO_ID, SHOP_REGION,
                        locations_by_region)
from .options import WeddingWitchOptions


class WeddingWitchItem(Item):
    game = GAME_NAME


class WeddingWitchLocation(Location):
    game = GAME_NAME


class WeddingWitchWeb(WebWorld):
    theme = "dirt"
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up Wedding Witch for Archipelago.",
        "English",
        "setup_en.md",
        "setup/en",
        ["jpn"],
    )]


class WeddingWitchWorld(World):
    """A bullet-heaven roguelite about a witch gathering potions, flowers and
    gifts for her wedding. Her permanent upgrades come from the multiworld
    instead of her shop."""

    game = GAME_NAME
    web = WeddingWitchWeb()
    options_dataclass = WeddingWitchOptions
    options: WeddingWitchOptions

    item_name_to_id = ITEM_NAME_TO_ID
    location_name_to_id = LOCATION_NAME_TO_ID
    item_name_groups = ITEM_NAME_GROUPS
    location_name_groups = LOCATION_NAME_GROUPS

    #: Universal Tracker rebuilds a seed without its yaml; _adopt_seed_options supplies
    #: the options the yaml would have.
    ut_can_gen_without_yaml = True

    @staticmethod
    def interpret_slot_data(slot_data: Dict[str, Any]) -> Dict[str, Any]:
        """Hands the seed's options back on re_gen_passthrough for _adopt_seed_options."""
        return slot_data

    def generate_early(self) -> None:
        self._adopt_seed_options()
        self._ensure_gifts()
        self.regions = locations_by_region(
            potion_slots=bool(self.options.potion_slot_locations),
            shop_slots=self.options.shop_locations.value)

        # Nightmare must not be the looser of the two gates.
        if self.options.nightmare_upgrades_required < self.options.hard_upgrades_required:
            self.options.nightmare_upgrades_required.value = \
                self.options.hard_upgrades_required.value

        self._clamp_upgrade_requirements()

    def _clamp_upgrade_requirements(self) -> None:
        """Clamps each requirement to the upgrade items placeable before its gate, less
        the Progressive Difficulty items the gate itself consumes.
        """
        before_hard = len(self.regions["Normal"]) + len(self.regions[SHOP_REGION])
        ceilings = {
            "hard_upgrades_required": before_hard - 1,
            "nightmare_upgrades_required": before_hard + len(self.regions["Hard"]) - 2,
        }

        for name, ceiling in ceilings.items():
            option = getattr(self.options, name)
            if option.value <= ceiling:
                continue
            logging.warning(
                "Wedding Witch (%s): %s lowered from %d to %d — only %d locations sit "
                "in front of that difficulty.",
                self.player_name, name, option.value, ceiling, ceiling + 1)
            option.value = ceiling

    def create_item(self, name: str) -> WeddingWitchItem:
        return WeddingWitchItem(name, classification(name), ITEM_NAME_TO_ID[name],
                                self.player)

    def _passthrough(self) -> Dict[str, Any]:
        """The live seed's slot data when a tracker is re-generating it, empty otherwise."""
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        return (passthrough or {}).get(GAME_NAME) or {}

    #: Options that decide which locations exist, rather than only how hard they are.
    SHAPE_OPTIONS = ("goal_forms", "shop_locations", "potion_slot_locations",
                     "hard_upgrades_required", "nightmare_upgrades_required")

    def _adopt_seed_options(self) -> None:
        """Applies the option values from slot data, so a tracker builds the locations
        the seed was rolled with rather than the defaults.
        """
        slot_data = self._passthrough()
        if not slot_data:
            return

        for name in self.SHAPE_OPTIONS:
            if name in slot_data:
                getattr(self.options, name).value = int(slot_data[name])

    def _ensure_gifts(self) -> None:
        """Builds the gift order on first use, since filler can be asked for before
        generate_early runs."""
        if getattr(self, "gift_order", None):
            return
        self.gift_order = self._gift_order()
        self.gifts_dealt = 0

    def _gift_order(self) -> List[int]:
        """The order gifts are dealt in.

        Not in slot data, so self.random is enough to keep it deterministic.
        """
        order = list(range(len(FILLER_ITEMS)))
        self.random.shuffle(order)
        return order

    def get_filler_item_name(self) -> str:
        """A different gift each call, then coins once the list is spent."""
        self._ensure_gifts()
        if self.gifts_dealt >= len(self.gift_order):
            return GOLD_ITEM
        name = FILLER_ITEMS[self.gift_order[self.gifts_dealt]]
        self.gifts_dealt += 1
        return name

    def create_items(self) -> None:
        pool: List[WeddingWitchItem] = [self.create_item(name)
                                        for name in item_pool_names()]

        total_locations = sum(len(names) for names in self.regions.values())
        if len(pool) > total_locations:
            raise OptionError(
                f"Wedding Witch has {len(pool)} items but only {total_locations} "
                f"locations. Raise Shop Locations or turn on Potion Slot Locations.")

        for _ in range(total_locations - len(pool)):
            pool.append(self.create_item(self.get_filler_item_name()))

        self.multiworld.itempool += pool

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu)

        for region_name, location_names in self.regions.items():
            region = Region(region_name, self.player, self.multiworld)
            region.add_locations({name: LOCATION_NAME_TO_ID[name]
                                  for name in location_names},
                                 WeddingWitchLocation)
            self.multiworld.regions.append(region)
            menu.connect(region, rule=self._region_rule(region_name))

    def _region_rule(self, region_name: str):
        # Shop stock only needs Love Coins, which any difficulty drops.
        if region_name == SHOP_REGION:
            return None

        difficulties = DIFFICULTY_REQUIREMENT[region_name]
        upgrades = {
            "Hard": self.options.hard_upgrades_required.value,
            "Nightmare": self.options.nightmare_upgrades_required.value,
        }.get(region_name, 0)

        if difficulties == 0 and upgrades == 0:
            return None

        player = self.player
        return lambda state: (
            state.has(PROGRESSIVE_DIFFICULTY, player, difficulties)
            and state.has_group(UPGRADE_ITEM_GROUP, player, upgrades)
        )

    def set_rules(self) -> None:
        self.multiworld.completion_condition[self.player] = \
            self._region_rule(GOAL_DIFFICULTY)

    def fill_slot_data(self) -> Dict[str, Any]:
        return {
            "goal_difficulty": GOAL_DIFFICULTY,
            "goal_forms": self.options.goal_forms.value,
            "shop_locations": self.options.shop_locations.value,
            "potion_slot_locations": bool(self.options.potion_slot_locations),
            "hard_upgrades_required": self.options.hard_upgrades_required.value,
            "nightmare_upgrades_required": self.options.nightmare_upgrades_required.value,
        }
