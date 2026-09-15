from test.bases import WorldTestBase

from .. import data
from ..world import BigWalkWorld


class BigWalkTestBase(WorldTestBase):
    game = "Big Walk"
    world: BigWalkWorld

    _collected_gourds = 0

    def collect_gourds(self, count: int) -> None:
        """
        Collect `count` more Gourd items than are already collected.

        Not `collect_by_name`: every gourd shares one name, so that helper
        collects the whole stack at once and any threshold test written with it
        would pass no matter what the rule says. Successive calls hand over
        gourds that have not been collected yet, so a test can walk a threshold
        one gourd at a time.
        """
        gourds = self.get_items_by_name(data.GOURD_ITEM_NAME)
        end = self._collected_gourds + count
        assert end <= len(gourds), f"only {len(gourds)} gourds in this pool, asked for {end}"
        self.collect(gourds[self._collected_gourds:end])
        self._collected_gourds = end
