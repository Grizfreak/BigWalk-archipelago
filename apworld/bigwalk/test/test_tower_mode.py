import unittest

from Options import OptionError

from .. import data, regions
from .bases import BigWalkTestBase, build_like_universal_tracker


class TestTheOrderIsDrawnOncePerSeed(BigWalkTestBase):
    options = {"tower_mode": "in_order", "gourd_sanity": "every_gourd", "require_arch_doors": True}
    run_default_tests = False

    def test_the_order_holds_every_tower_once(self) -> None:
        order = self.world.tower_order
        self.assertEqual(sorted(tower.prop_name for tower in data.TOWERS), sorted(order))

    def test_the_slot_data_carries_the_order_and_the_mode(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(list(self.world.tower_order), slot_data["tower_order"])
        self.assertEqual("in_order", slot_data["tower_mode"])

    def test_the_first_tower_costs_only_gourds_and_its_door(self) -> None:
        first = data.tower_by_prop(self.world.tower_order[0])
        door = data.TOWER_FAR_DOOR.get(first.prop_name)
        self.collect_gourds(first.slots)
        if door is not None and door in self.world.locked_arch_doors:
            self.assertFalse(self.can_reach_location(data.deposit_location_name(first.slots)))
            self.collect_by_name(door.item_name)
        self.collect_by_name(data.DRAWBRIDGE_ITEM_NAME)
        self.collect_by_name(data.FIRST_ARCH_DOOR.item_name)
        self.assertTrue(self.can_reach_location(data.deposit_location_name(first.slots)))

    def test_a_gourd_past_the_first_tower_needs_the_gourds_of_all_the_way(self) -> None:
        first = data.tower_by_prop(self.world.tower_order[0])
        self.collect_all_but(data.GOURD_ITEM_NAME)
        self.collect_gourds(first.slots)
        self.assertFalse(self.can_reach_location(data.deposit_location_name(first.slots + 1)))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location(data.deposit_location_name(first.slots + 1)))

    def test_deposits_of_the_drawbridge_are_in_the_starting_zone_only_if_it_comes_first(self) -> None:
        first = self.world.tower_order[0]
        deposit = self.multiworld.get_location(data.deposit_location_name(1), self.player)
        expected = regions.STARTING_ZONE if first == "bigKeyIntro" else regions.OVERWORLD
        self.assertEqual(expected, deposit.parent_region.name)


class TestByItemHoldsOneFewerMonumentThanTowers(BigWalkTestBase):
    options = {"tower_mode": "by_item", "gourd_sanity": "every_gourd"}
    run_default_tests = False

    def test_six_progressive_monuments_are_in_the_pool(self) -> None:
        count = len(self.get_items_by_name(data.PROGRESSIVE_MONUMENT_ITEM_NAME))
        self.assertEqual(len(data.TOWERS) - 1, count)

    def test_the_second_tower_waits_for_the_first_item(self) -> None:
        first = data.tower_by_prop(self.world.tower_order[0])
        self.collect_all_but(data.PROGRESSIVE_MONUMENT_ITEM_NAME)
        self.assertFalse(self.can_reach_location(data.deposit_location_name(first.slots + 1)))
        self.collect(self.get_items_by_name(data.PROGRESSIVE_MONUMENT_ITEM_NAME)[:1])
        self.assertTrue(self.can_reach_location(data.deposit_location_name(first.slots + 1)))

    def test_the_first_tower_needs_no_item(self) -> None:
        first = data.tower_by_prop(self.world.tower_order[0])
        self.collect_all_but(data.PROGRESSIVE_MONUMENT_ITEM_NAME)
        self.assertTrue(self.can_reach_location(data.deposit_location_name(first.slots)))


class TestTheDepositsOfATowerAreHinted(BigWalkTestBase):
    options = {"tower_mode": "in_order", "gourd_sanity": "every_gourd", "hint_discovered_deposit_boxes": True}
    run_default_tests = False

    def test_each_tower_lists_the_deposits_of_its_slots(self) -> None:
        hints = self.world.fill_slot_data()["tower_reach_hints"]
        start = 0
        for prop in self.world.tower_order:
            tower = data.tower_by_prop(prop)
            if prop in hints:
                expected = {data.deposit_location_id(amount) for amount in range(start + 1, start + tower.slots + 1)}
                self.assertEqual(expected, set(hints[prop]))
            start += tower.slots

    def test_the_drawbridge_and_the_dome_are_hinted_too(self) -> None:
        # Reached by position in the mod (the player, 2026-10-12), so their deposits are hinted like the others'.
        hints = self.world.fill_slot_data()["tower_reach_hints"]
        self.assertIn("bigKeyIntro", hints)
        self.assertIn("bigKeyOverflow", hints)


class TestNoMonumentOpenAtTheStart(BigWalkTestBase):
    options = {"tower_mode": "by_item", "first_tower_open": False, "gourd_sanity": "every_gourd"}
    run_default_tests = False

    def test_seven_progressive_monuments_are_in_the_pool(self) -> None:
        self.assertEqual(len(data.TOWERS), len(self.get_items_by_name(data.PROGRESSIVE_MONUMENT_ITEM_NAME)))

    def test_the_first_gourd_waits_for_an_item(self) -> None:
        self.collect_all_but(data.PROGRESSIVE_MONUMENT_ITEM_NAME)
        self.assertFalse(self.can_reach_location(data.deposit_location_name(1)))
        self.collect(self.get_items_by_name(data.PROGRESSIVE_MONUMENT_ITEM_NAME)[:1])
        self.assertTrue(self.can_reach_location(data.deposit_location_name(1)))

    def test_the_slot_data_says_so(self) -> None:
        self.assertFalse(self.world.fill_slot_data()["first_tower_open"])


class TestTheFirstTowerNeedsByItem(unittest.TestCase):
    def test_in_order_with_no_first_tower_is_refused(self) -> None:
        with self.assertRaises(OptionError):
            build_like_universal_tracker({"tower_mode": "in_order", "first_tower_open": False})


class TestTheDomeGivesUpWhatIsMissing(BigWalkTestBase):
    options = {"tower_mode": "in_order"}
    run_default_tests = False

    def test_with_all_the_gourds_every_tower_keeps_its_slots(self) -> None:
        for tower in data.TOWERS:
            self.assertEqual(tower.slots, self.world.tower_slots(tower))

    def test_with_fewer_gourds_only_the_dome_is_short(self) -> None:
        self.world.gourd_count = data.MAX_MONUMENT_SLOTS - 5
        for tower in data.TOWERS:
            if tower.prop_name == "bigKeyOverflow":
                self.assertEqual(tower.slots - 5, self.world.tower_slots(tower))
            else:
                self.assertEqual(tower.slots, self.world.tower_slots(tower))

    def test_the_slot_data_carries_the_dome(self) -> None:
        self.assertEqual(15, self.world.fill_slot_data()["green_dome_slots"])


class TestTheTowersObjective(BigWalkTestBase):
    options = {"tower_mode": "in_order", "goal": "big_game", "objectives": ["big_towers"], "towers_required": 2}
    run_default_tests = False

    def test_victory_needs_the_gourds_of_two_towers_and_the_ending(self) -> None:
        capacity = sum(data.tower_by_prop(prop).slots for prop in self.world.tower_order[:2])
        self.collect_all_but(data.GOURD_ITEM_NAME)
        self.collect_gourds(capacity - 1)
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location("Victory"))

    def test_the_slot_data_carries_the_count(self) -> None:
        self.assertEqual(2, self.world.fill_slot_data()["towers_required"])
        self.assertEqual(["big_game", "big_towers"], self.world.fill_slot_data()["objectives"])


class TestAllOpenIsWhatItWas(BigWalkTestBase):
    run_default_tests = False

    def test_no_order_no_item(self) -> None:
        self.assertEqual((), self.world.tower_order)
        self.assertEqual([], self.get_items_by_name(data.PROGRESSIVE_MONUMENT_ITEM_NAME))
        self.assertEqual([], self.world.fill_slot_data()["tower_order"])


class TestTheTowersObjectiveNeedsAMode(unittest.TestCase):
    def test_all_open_is_refused(self) -> None:
        with self.assertRaises(OptionError):
            build_like_universal_tracker({"goal": "big_game", "objectives": ["big_towers"]})

    def test_the_tracker_reads_the_order_back(self) -> None:
        first = build_like_universal_tracker({"tower_mode": "in_order"})
        order = first.worlds[1].tower_order
        slot_data = {"tower_mode": "in_order", "tower_order": list(order)}
        again = build_like_universal_tracker({"tower_mode": "in_order"}, slot_data)
        self.assertEqual(order, again.worlds[1].tower_order)


def _sweep(mode: str, seed: int) -> type:
    """A whole generation, default checks included, for one seed and one mode: nothing may fail to fill."""

    class Sweep(BigWalkTestBase):
        options = {"tower_mode": mode}
        game = "Big Walk"

    Sweep.seed = seed
    Sweep.__name__ = f"TestSweep_{mode}_{seed}"
    Sweep.__qualname__ = Sweep.__name__
    return Sweep


for _mode in ("in_order", "by_item"):
    for _seed in range(12):
        _cls = _sweep(_mode, _seed)
        globals()[_cls.__name__] = _cls


def _sweep_closed(seed: int) -> type:
    """A whole generation with no monument open at the start: it must fill."""

    class SweepClosed(BigWalkTestBase):
        options = {"tower_mode": "by_item", "first_tower_open": False}
        game = "Big Walk"

    SweepClosed.seed = seed
    SweepClosed.__name__ = f"TestSweep_by_item_closed_{seed}"
    SweepClosed.__qualname__ = SweepClosed.__name__
    return SweepClosed


for _seed in range(12):
    _cls = _sweep_closed(_seed)
    globals()[_cls.__name__] = _cls
del _cls


class TestATowerRoofWaitsForItsTurn(BigWalkTestBase):
    # The mod holds a tower's opening button until its monument opens: the roof and the button's check wait for it.
    options = {"tower_mode": "in_order", "gourd_sanity": "every_gourd", "tower_sanity": True,
               "firework_sanity": True, "object_sanity": "every"}
    run_default_tests = False

    def test_the_roof_of_a_later_tower_needs_the_gourds_before_it(self) -> None:
        order = self.world.tower_order
        prop = next(prop for prop in order[1:] if any(f.tower == prop for f in data.FIREWORKS))
        before = sum(self.world.tower_slots(data.tower_by_prop(p)) for p in order[:order.index(prop)])
        roof = [next(f.location_name for f in data.FIREWORKS if f.tower == prop),
                data.tower_reached_location_name(data.tower_by_prop(prop))]
        roof += [obj.location_name for obj in data.ISLAND_OBJECTS
                 if obj.zone == f"tower:{prop}" and obj.kind not in data.LAMP_KINDS]
        foot = [obj.location_name for obj in data.ISLAND_OBJECTS
                if obj.zone == f"tower:{prop}" and obj.kind in data.LAMP_KINDS]

        self.collect_all_but(data.GOURD_ITEM_NAME)
        self.collect_gourds(before - 1)
        for name in roof:
            self.assertFalse(self.can_reach_location(name), name)
        for name in foot:
            self.assertTrue(self.can_reach_location(name), name)
        self.collect_gourds(1)
        for name in roof:
            self.assertTrue(self.can_reach_location(name), name)
