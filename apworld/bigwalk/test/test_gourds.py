from .. import data
from .bases import BigWalkTestBase


class TestDefaultPool(BigWalkTestBase):
    options: dict = {}

    def test_one_gourd_per_monument_slot(self) -> None:
        # The pool must be able to fill every monument, or the last deposit
        # checks become impossible to reach: 45.
        self.assertEqual(len(self.get_items_by_name(data.GOURD_ITEM_NAME)),
                         data.MAX_MONUMENT_SLOTS)

    def test_drawbridge_is_shuffled_by_default(self) -> None:
        # The default flipped on 2026-09-22, once the tutorial was confirmed
        # to have another way out: the Drawbridge is a feature like the other
        # six and is found, not given.
        self.assertTrue(self.get_items_by_name(data.DRAWBRIDGE_ITEM_NAME))
        precollected = [item.name for item in self.multiworld.precollected_items[self.player]]
        self.assertNotIn(data.DRAWBRIDGE_ITEM_NAME, precollected)

    def test_drawbridge_deposit_needs_its_key_and_no_gourds(self) -> None:
        # Starting with the drawbridge OPEN says nothing about the key that
        # fits it: the feature lets you leave the tutorial, the key is six
        # checks. So this location is unreachable until the key itself arrives,
        # and no quantity of gourds substitutes for it.
        drawbridge = data.TOWERS[0]
        self.assertFalse(self.can_reach_location("Drawbridge Key Deposit"))
        self.collect_gourds(4)
        self.assertFalse(self.can_reach_location("Drawbridge Key Deposit"))
        self.collect_by_name(data.key_item_name(drawbridge))
        self.assertTrue(self.can_reach_location("Drawbridge Key Deposit"))

    def test_puzzles_need_nothing(self) -> None:
        # No received item gates a puzzle *by being a puzzle*: solving one is
        # always possible once you can stand in front of it. The exception is
        # geography, not logic — the purple gourds are past the chairlift, so
        # they are excluded here and covered by test_regions.py instead.
        gated = set(data.chairlift_locations()) | set(data.tunnel_locations())
        for puzzle in data.PUZZLES:
            if puzzle.location_name in gated:
                continue
            self.assertTrue(self.can_reach_location(puzzle.location_name), puzzle.location_name)


class TestStartWithDrawbridgeOpen(BigWalkTestBase):
    """The opt-in: handed over up front, and then absent from the pool."""

    options = {"start_with_drawbridge_open": True}
    run_default_tests = False

    def test_drawbridge_is_precollected_not_shuffled(self) -> None:
        self.assertFalse(self.get_items_by_name(data.DRAWBRIDGE_ITEM_NAME))
        precollected = [item.name for item in self.multiworld.precollected_items[self.player]]
        self.assertIn(data.DRAWBRIDGE_ITEM_NAME, precollected)


class TestMonumentsBuyOnlyTheirOwnChecks(BigWalkTestBase):
    """
    Filling a monument no longer releases a key (decided 2026-09-21).

    Until then a tower's key appeared once its monument was full, so every key
    deposit was charged a running total of gourds — a faithful model of the
    vanilla game. The keys are Archipelago items now, and a monument buys
    nothing but its own deposit checks. These tests exist to make sure the old
    coupling cannot come back by accident.
    """

    options: dict = {}
    run_default_tests = False

    def test_no_amount_of_gourds_reaches_a_key_deposit(self) -> None:
        self.collect_gourds(data.MAX_MONUMENT_SLOTS)
        for tower in self.world.towers:
            self.assertFalse(self.can_reach_location(tower.location_name),
                             tower.location_name)

    def test_no_amount_of_gourds_reaches_a_cut(self) -> None:
        self.collect_gourds(data.MAX_MONUMENT_SLOTS)
        for tower in self.world.towers:
            for name in data.cut_locations(tower):
                self.assertFalse(self.can_reach_location(name), name)

    def test_gourd_deposits_still_cost_gourds(self) -> None:
        # The one thing a monument IS still for. Counted in fives because the
        # default `deposit_locations` is milestones, so 1..4 are not locations.
        self.collect_gourds(5)
        self.assertTrue(self.can_reach_location(data.deposit_location_name(5)))
        self.assertFalse(self.can_reach_location(data.deposit_location_name(10)))


class TestShuffledTutorialKey(BigWalkTestBase):
    options = {"start_with_drawbridge_open": False}
    run_default_tests = False

    def test_drawbridge_is_in_the_pool_and_its_key_is_a_separate_item(self) -> None:
        drawbridge = data.TOWERS[0]
        self.assertTrue(self.get_items_by_name(data.DRAWBRIDGE_ITEM_NAME))
        self.assertTrue(self.get_items_by_name(data.key_item_name(drawbridge)))
        self.assertNotEqual(data.DRAWBRIDGE_ITEM_NAME, data.key_item_name(drawbridge))


class TestKeyCuts(BigWalkTestBase):
    """
    The 25 forage checks: one per segment cut out of a big key.

    Measured in game on 2026-09-21 rather than taken from the third-party
    document — five each on the drawbridge and the four coloured towers, and
    none at all on the Black Monolith and Green Dome keys, which are born
    finished.
    """

    options: dict = {}
    run_default_tests = False

    def test_twenty_five_of_them_exist(self) -> None:
        self.assertEqual(data.TOTAL_CUT_SEGMENTS, 25)
        names = {location.name for location in self.multiworld.get_locations(self.player)}
        for tower in data.TOWERS:
            for name in data.cut_locations(tower):
                self.assertIn(name, names, name)

    def test_the_finished_keys_have_none(self) -> None:
        # bigKeyBoss and bigKeyOverflow have `covers = 0` and their Complete
        # PropGroup already in propGroups: there is nothing to cut, so a
        # location for it would be one the game can never check.
        self.assertEqual(data.BLACK_MONOLITH.segments, 0)
        self.assertEqual(data.GREEN_DOME.segments, 0)
        self.assertEqual(data.cut_locations(data.BLACK_MONOLITH), ())
        self.assertEqual(data.cut_locations(data.GREEN_DOME), ())

    def test_ids_are_unique_and_clear_of_every_other_range(self) -> None:
        from .. import locations as bigwalk_locations

        ids = [data.cut_location_id(tower, index)
               for tower in data.TOWERS for index in range(tower.segments)]
        self.assertEqual(len(ids), len(set(ids)))

        # Against the whole table rather than against a remembered range: the
        # point is that no cut id collides with a puzzle, a key deposit, a
        # radio station or a gourd deposit, whatever those happen to be.
        by_id: dict[int, list[str]] = {}
        for name, location_id in bigwalk_locations.LOCATION_NAME_TO_ID.items():
            by_id.setdefault(location_id, []).append(name)
        for location_id, names in by_id.items():
            self.assertEqual(len(names), 1, f"id {location_id} is shared by {names}")

    def test_a_cut_costs_its_own_key_and_nothing_else(self) -> None:
        # Cutting a key means holding it, and a key is an Archipelago item. All
        # six of a tower's locations rest on that one requirement.
        first_cut = data.cut_location_name(data.GREEN_CUP, 0)
        self.assertFalse(self.can_reach_location(first_cut))

        self.collect_by_name(data.key_item_name(data.GREEN_CUP))
        self.assertTrue(self.can_reach_location(first_cut))
        self.assertTrue(self.can_reach_location(data.GREEN_CUP.location_name))

        # And it buys that tower only.
        self.assertFalse(self.can_reach_location(data.cut_location_name(data.YELLOW_TWIST, 0)))


class TestFeatureItems(BigWalkTestBase):
    """The item is the feature, not the key (decided 2026-09-21)."""

    options: dict = {}
    run_default_tests = False

    def test_every_tower_offers_its_feature(self) -> None:
        from .. import items as bigwalk_items

        self.assertEqual(
            bigwalk_items.ITEM_NAME_GROUPS["Features"],
            {tower.item_name for tower in data.TOWERS},
        )

    def test_the_three_that_gate_a_region_are_progression(self) -> None:
        from BaseClasses import ItemClassification

        from .. import items as bigwalk_items

        for tower in (data.GREEN_CUP, data.YELLOW_TWIST, data.BLACK_MONOLITH):
            self.assertEqual(bigwalk_items.classification_for(tower.item_name),
                             ItemClassification.progression, tower.item_name)

    def test_no_item_is_still_named_after_a_key(self) -> None:
        # The old names ("Green Cup Key", "Tutorial Key") described a thing the
        # player no longer receives. A leftover would be a plain lie about what
        # the item does, so it is asserted rather than trusted to review.
        for tower in data.TOWERS:
            self.assertNotIn("Key", tower.item_name, tower.item_name)
