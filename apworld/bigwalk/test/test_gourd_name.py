"""gourd_name."""

from unittest import TestCase

import worlds
from worlds.AutoWorld import data_package_checksum

from .. import data, gourd_name, items
from .bases import BigWalkTestBase

NAME = "Plumbus du Destin"


def embedded_multidata() -> dict:
    # Main.py embeds the shared package object itself.
    return {"datapackage": {"Big Walk": worlds.network_data_package["games"]["Big Walk"]}}


class TestDefault(BigWalkTestBase):
    run_default_tests = False

    def test_nothing_is_renamed(self) -> None:
        self.assertEqual(self.world.gourd_name, data.GOURD_ITEM_NAME)
        self.assertEqual(self.world.fill_slot_data()["gourd_name"], data.GOURD_ITEM_NAME)

    def test_the_data_package_is_left_alone(self) -> None:
        multidata = embedded_multidata()
        package = multidata["datapackage"]["Big Walk"]
        self.world.modify_multidata(multidata)
        self.assertIs(multidata["datapackage"]["Big Walk"], package)


class TestRenamed(BigWalkTestBase):
    options = {"gourd_name": NAME}

    def test_the_world_keeps_the_real_name(self) -> None:
        self.assertEqual(self.world.gourd_name, NAME)
        self.assertEqual(len(self.get_items_by_name(data.GOURD_ITEM_NAME)), data.MAX_MONUMENT_SLOTS)
        self.assertEqual(self.world.fill_slot_data()["gourd_name"], NAME)

    def test_the_data_package_gets_a_renamed_copy(self) -> None:
        original = worlds.network_data_package["games"]["Big Walk"]
        before = dict(original["item_name_to_id"])
        multidata = embedded_multidata()
        self.world.modify_multidata(multidata)
        package = multidata["datapackage"]["Big Walk"]

        gourd_id = items.ITEM_NAME_TO_ID[data.GOURD_ITEM_NAME]
        self.assertEqual(package["item_name_to_id"][NAME], gourd_id)
        self.assertNotIn(data.GOURD_ITEM_NAME, package["item_name_to_id"])
        self.assertEqual(len(package["item_name_to_id"]), len(before))
        self.assertEqual(package["item_name_groups"][data.GOURD_ITEM_NAME], [NAME])
        self.assertIn(NAME, package["item_name_groups"]["Everything"])
        self.assertEqual(list(package["item_name_groups"]), sorted(package["item_name_groups"]))
        # archipelago.gg recomputes it on upload.
        unchecked = {key: value for key, value in package.items() if key != "checksum"}
        self.assertEqual(package["checksum"], data_package_checksum(unchecked))
        self.assertNotEqual(package["checksum"], original["checksum"])
        # The shared package must stay untouched.
        self.assertIs(worlds.network_data_package["games"]["Big Walk"], original)
        self.assertEqual(original["item_name_to_id"], before)

    def test_a_second_slot_finds_it_done(self) -> None:
        multidata = embedded_multidata()
        self.world.modify_multidata(multidata)
        renamed = multidata["datapackage"]["Big Walk"]
        self.world.modify_multidata(multidata)
        self.assertIs(multidata["datapackage"]["Big Walk"], renamed)

    def test_the_spoiler_says_the_new_name(self) -> None:
        gourd = self.world.create_item(data.GOURD_ITEM_NAME)
        gourd.location = self.multiworld.get_location("Drawbridge Key Deposit", self.player)
        self.assertEqual(repr(gourd), NAME)
        self.assertEqual(gourd.name, data.GOURD_ITEM_NAME)
        backpack = self.world.create_item("Backpack")
        backpack.location = gourd.location
        self.assertEqual(repr(backpack), "Backpack")


class TestRefused(BigWalkTestBase):
    options = {"gourd_name": "backpack"}
    run_default_tests = False

    def test_a_name_of_the_game_is_refused(self) -> None:
        self.assertEqual(self.world.gourd_name, data.GOURD_ITEM_NAME)


class TestCleaning(TestCase):
    taken = (*items.ITEM_NAME_TO_ID, *items.ITEM_NAME_GROUPS)

    def clean(self, wanted: str) -> str:
        return gourd_name.clean(wanted, self.taken, "Tester")

    def test_trimmed(self) -> None:
        self.assertEqual(self.clean("  Big   Boid "), "Big Boid")

    def test_cut_to_the_mods_limit(self) -> None:
        self.assertEqual(self.clean("x" * 40), "x" * gourd_name.MAX_LENGTH)

    def test_empty_or_gourd_is_gourd(self) -> None:
        for wanted in ("", "   ", "gourd", "GOURD"):
            self.assertEqual(self.clean(wanted), data.GOURD_ITEM_NAME, wanted)

    def test_items_and_groups_are_taken(self) -> None:
        for wanted in ("Backpack", "PLUMBUS", "traps", "Gourd Carton"):
            self.assertEqual(self.clean(wanted), data.GOURD_ITEM_NAME, wanted)


class TestShared(TestCase):
    def test_one_name_for_all(self) -> None:
        self.assertEqual(gourd_name.shared({"A": NAME, "B": NAME}), NAME)

    def test_disagreeing_slots_keep_gourd(self) -> None:
        self.assertEqual(gourd_name.shared({"A": NAME, "B": "Boyo"}), data.GOURD_ITEM_NAME)
        self.assertEqual(gourd_name.shared({"A": NAME, "B": data.GOURD_ITEM_NAME}), data.GOURD_ITEM_NAME)

    def test_no_slot(self) -> None:
        self.assertEqual(gourd_name.shared({}), data.GOURD_ITEM_NAME)
