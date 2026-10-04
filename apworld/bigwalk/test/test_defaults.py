"""A seed with every option at its default: it generates, and it can be won."""

from test.bases import WorldTestBase


class TestTheRealDefaults(WorldTestBase):
    # Not BigWalkTestBase: that one turns some defaults off for the other tests.
    game = "Big Walk"

    def test_the_puzzles_parts_are_items(self) -> None:
        self.assertTrue(self.world.options.lock_puzzle_needs)
        self.assertTrue(self.world.options.require_arch_doors)
