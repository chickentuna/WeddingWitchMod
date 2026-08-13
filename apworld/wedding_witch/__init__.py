"""Archipelago world for Wedding Witch.

The mod stops the meta-upgrade shop selling upgrades, so every one of its 73 purchasable
levels is an Archipelago item, and restocks its shelves with checks. Checks come from
the potions offered after a stage, from clearing a difficulty's boss in each of the six
witch forms, and from those shelves.
"""
import logging
from typing import Any, Dict, List

from BaseClasses import Item, Location, Region, Tutorial
from Options import OptionError
from worlds.AutoWorld import WebWorld, World

from .constants import (DIFFICULTY_REQUIREMENT, FILLER_ITEMS, GAME_NAME,
                        GOAL_DIFFICULTY, PROGRESSIVE_DIFFICULTY,
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

    #: Lets Universal Tracker rebuild a seed without the yaml it was rolled from. UT
    #: generates against an empty yaml in that case, so every option starts at its
    #: default and the seed's real values have to come off the slot data — which is what
    #: _adopt_seed_options is for. Setting this without that would track the wrong world.
    ut_can_gen_without_yaml = True

    @staticmethod
    def interpret_slot_data(slot_data: Dict[str, Any]) -> Dict[str, Any]:
        """What opts this world into Universal Tracker's re-generation.

        Returning the slot data is the signal to re-run generation and hand it back on
        multiworld.re_gen_passthrough, which _adopt_seed_options reads.
        """
        return slot_data

    def generate_early(self) -> None:
        self._adopt_seed_options()
        self._ensure_gifts()
        self.regions = locations_by_region(
            potion_slots=bool(self.options.potion_slot_locations),
            shop_slots=self.options.shop_locations.value)

        # Nightmare sitting below Hard would make the harder difficulty the looser
        # gate, which is never what someone setting these meant.
        if self.options.nightmare_upgrades_required < self.options.hard_upgrades_required:
            self.options.nightmare_upgrades_required.value = \
                self.options.hard_upgrades_required.value

        self._clamp_upgrade_requirements()

    def _clamp_upgrade_requirements(self) -> None:
        """A requirement past what is collectable before its gate can never be met.

        The options are ranged against the whole upgrade tree, but how much of it a
        seed can hand you before Hard opens depends on how many locations sit in front
        of Hard — so the real ceiling is the seed's, not the tree's. Each gate also has
        to leave room for the Progressive Difficulty items that open it.
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
        """The live seed's slot data, when a tracker is re-generating it.

        Empty during a real generation, which is what makes every caller here fall back
        to rolling or to the yaml.
        """
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        return (passthrough or {}).get(GAME_NAME) or {}

    #: Options that decide which locations exist, rather than only how hard they are.
    SHAPE_OPTIONS = ("goal_forms", "shop_locations", "potion_slot_locations",
                     "hard_upgrades_required", "nightmare_upgrades_required")

    def _adopt_seed_options(self) -> None:
        """Take the world's shape from the seed being played, not from the yaml in front
        of us.

        Universal Tracker hands the slot data over but does not apply it to options, and
        on the ut_can_gen_without_yaml path it generates against an empty yaml — so every
        option arrives at its default. Shop Locations and Potion Slot Locations decide
        which locations exist at all, so taking the defaults would have the tracker
        listing locations the seed never had.
        """
        slot_data = self._passthrough()
        if not slot_data:
            return

        for name in self.SHAPE_OPTIONS:
            if name in slot_data:
                getattr(self.options, name).value = int(slot_data[name])

    def _ensure_gifts(self) -> None:
        """Item-link replacement asks a world for filler without running generate_early,
        so the order has to be able to build itself on first use rather than assume it."""
        if getattr(self, "gift_order", None):
            return
        self.gift_order = self._gift_order()
        self.gifts_dealt = 0

    def _gift_order(self) -> List[int]:
        """The order gifts are dealt in.

        Rolled freely: a tracker rebuilding this seed stops before items are placed, so
        the pool it builds is never read and the order it lands on cannot be seen. What a
        tracker does need is every gift name in item_name_to_id, so a received one
        resolves to a name rather than a bare id.
        """
        order = list(range(len(FILLER_ITEMS)))
        self.random.shuffle(order)
        return order

    def get_filler_item_name(self) -> str:
        """Dealt in order rather than drawn one at a time, so a seed with room for a
        dozen gifts shows a dozen different ones."""
        self._ensure_gifts()
        name = FILLER_ITEMS[self.gift_order[self.gifts_dealt % len(self.gift_order)]]
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

        # Whatever is left over is a wedding gift, dealt in order so a seed with room for
        # a dozen shows a dozen different ones.
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
        # The mod is what actually finishes the slot — it sends the goal once enough of
        # the goal difficulty's boss-form locations are checked. Nothing on this side
        # can observe which form the witch is in, so generation only needs to know the
        # point past which that run is a fair ask.
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
