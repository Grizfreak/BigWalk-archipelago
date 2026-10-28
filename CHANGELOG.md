# Changelog

The mod and the apworld move together: `Plugin.PluginVersion`, the `.csproj`,
`archipelago.json` and `WORLD_VERSION` carry the same number. A seed made by
an older apworld plays with a newer mod (ids never change); the other way
round, an older mod ignores what it does not know and can make a seed
unwinnable, which is why every player of a session should run the same build.

## 1.0.4

### Moving from 1.0.3 to 1.0.4

- **Everyone needs this build** with `lock_abilities`: the lock of the chairlift and the train travels to the guests with the others.
- A 1.0.3 YAML generates as it did, except with `lock_abilities` and `object_sanity`: the Megaphones now ask for
  Carrying.

### Fixed

- **The Carousel's swings could be sat on without Seat** (Tyrope): a seat that drives the puzzle was never locked. The
  logic already asked for Seat; the mod now does too.
- **A deck chair picked up first could be sat on without Seat** (naf): a chair you can carry is now always furniture,
  even in the hands of the player who holds it.
- **The Megaphones were in logic without Carrying** (Tyrope): each one hangs above one player's reach.

### Added

- **`rides_require_seat`** (off by default): with `lock_abilities`, the chairlift and the train are locked like any
  seat until Seat arrives, and the logic asks for Seat to use them.

## 1.0.3

### Moving from 1.0.2 to 1.0.3

- **Everyone needs this build:** the copies of received gadgets are painted from their network id, and a guest on 1.0.2
  would paint them another way than its host.
- A 1.0.2 YAML generates as it did. With `tower_mode`, a tower's roof (its gadgets and fireworks) and "Reached the
  tower" now wait in logic for the tower's turn, so such a seed may place things differently. `big_fiesta` is now
  `big_party`; the old name is still read, in a YAML, a seed and a save.

### Fixed

- **The host's game hitched every couple of seconds**, worse the longer the session ran. Each received gadget made the
  mod look through every prop of the world again, and a few checks ran all in the same frame every two seconds. The
  look happens once per world now, and the checks one per frame.
- **A guest's big fall never sent a DeathLink** (`big_fall`): only the host's falls were seen. The host now sees every
  player's.
- **With `tower_mode`, the trackers showed a tower's roof in logic before the tower opened**: its gadgets, its
  fireworks and "Reached the tower" now wait for its turn, as the button at its foot does. The lamps around a tower are
  at its foot and do not wait.

### Added

- **`GadgetCopiesKept`** (in the mod's `.cfg`, 10 by default): at most that many copies of each received gadget in the
  world. Loading a save puts back no more, and past it a new one takes the place of the oldest left lying around. A
  long session no longer piles up hundreds of them.
- With `random_colors`, every gadget takes its own color, each copy received too, and a torch's light takes its color.
- **Big Confetti**, a bonus (`bonus_weights`): two bursts of a puzzle's confetti around every player, with the sound of
  a puzzle won.
- `big_fiesta` is now **`big_party`** ("Big Party (on the beach)"), and `big_club` reads as `big_doof`.

## 1.0.2

### Moving from 1.0.1 to 1.0.2

- **Everyone needs this build:** the gadgets the multiworld sends are built differently, and a guest on 1.0.1 would
  build them another way than its host.
- A 1.0.1 YAML generates as it did. With `open_black_tower` off, the Black Tower's button and monument now wait in
  logic for the door at its foot, so such a seed may place things differently.

### Fixed

- **Gadgets received with `object_sanity` could break between players:** a scanner nobody else could pick up, then
  stuck on a guest's face, a torch that stayed on, a walkie-talkie that gave a guest their own voice back. A received
  gadget could borrow what identifies one of the island's own, which stays on the map with `object_sanity`.
- **Opening the door of a light puzzle sent a DeathLink** (Pointer's Paradise and the others whose lights must be
  pressed in order): the game puts the lights out when the room closes, the same way it does for a wrong press. Only a
  press counts now.
- **Peg tiles were shuffled across puzzles:** in the Green Tower, the Ring Room, the Silent Gauntlet and the rooms
  around them, tiles were strewn about, doubled in one puzzle and missing in another, invisible ink in a plain puzzle
  and plain tiles in the ink one. Tiles now swap only within their own rack and their own kind, a few seconds after the world loads, and the same
  way every time the save is loaded.
- **A key's cutting flags on the map followed the key around:** a key carried near another trail showed its flags on
  that trail's stations. Each key's flags stay on its own trail. (A key can be cut along any trail, as in the game.)
- **With `open_black_tower` off, the Black Tower's button and monument were in logic behind a shut door.** They now
  wait for it: the monuments of the drawbridge and the four coloured towers full, or with `tower_mode` the Black
  Tower's turn, when the mod opens it.
- Generation is much faster in large multiworlds: the world's pre-fill no longer walks every other world (27 s down
  to under a tenth of a second per Big Walk among 150 slots).

### Added

- **`mirrored_poses`** (0 to 100, off by default): the pose panels (Indoor Semaphore, Optical Telegraph, Charades
  Rooms) are drawn among the game's poses and the mirrors of the four that are not symmetric, a pose and its mirror
  possibly side by side. The value is how often a mirror is drawn against a game pose. The puzzles work as before.
- **Two objectives in the tunnels:** **`big_doof`**, the Doof Chamber's five lights on and its smoke machine's button held for ten seconds, and
  **`big_makeover`**, painted at the Secret Salon (every player, each in turn, with `everyone_gathers`). With `everyone_gathers`, every player there. The Doof
  Chamber's switches and radio are now in the tunnels for the logic and for `zone_locks`.
- Big Climb's, Big Fiesta's and Big Doof's ten seconds tick, with Cabin Fever's sound: once when they start, then on
  each of the last three, for every player.
- With `random_colors`, each laser takes its own color, its tip and its beam included.
- With `tower_mode: by_item`, the towers' line says which Progressive Monument it waits for.
- The host is told on screen when the seed was made with a newer apworld than the mod: the mod ignores what it does not
  know, which can leave a seed unwinnable. A seed from an older apworld plays as it was generated, without the newer
  options (custom tiles among them), and is not warned about.
- The session journal (`BepInEx/session-journal.tsv`) notes what each player holds, for reports of a gourd that cannot
  be picked up.
- The setup guide covers Linux and the Steam Deck (Proton needs a launch option).

## 1.0.1

### Moving from 1.0.0 to 1.0.1

- **A fix for the mod only.** Nothing changed in the world: a 1.0.0 seed plays as it is, and so does a seed already
  started (a seed stuck at a shut Green Dome opens again once the host runs 1.0.1). Every player of a session should
  still run the same build.

### Fixed

- **The Green Dome's door stayed shut** while its monument was the one taking gourds: the game opens it once the
  Black Tower is complete, which the world never asked for. With `tower_mode` drawing the Green Dome before the Black
  Tower, no monument took gourds at all. The mod now opens it at the Green Dome's turn (from the start with
  `all_open`).
- **A player who fell could not get up** with Legs and Seat both locked (`lock_abilities`): jump and sit, the only
  ways up, stay on while seated.
- A raised arm that pointed, with pointing locked, still showed a T-pose and its sound on the player's own screen.

## 1.0.0

### Moving from 0.7 to 1.0

- **Everyone needs this build:** the host's channel to its guests changed (protocol v22).
- **A 0.7 YAML generates as it did, with one change you can see:** `custom_tiles` is on by default
  (`matching_games`): when a game of the multiworld has a drawing of its own, some of the puzzles' basic tiles show it.
  The puzzles ask for the same tiles; `custom_tiles: off` keeps the game's.
- `pack_sanity`, `flare_gun_sanity` and `linear_towers` were on/off and have more values now; `true` and `false`
  still mean what they meant.

### Added

- **Custom tiles**, drawn by the community (Jack5, Don Chalant, GameFraek, Kensufox; CC BY 4.0): the nine basic peg
  tiles shown as other drawings, on every tile, split-flap and display, the same on every player's screen.
  **`custom_tiles`** (off / matching_games, the default: the drawings of the games in the multiworld join the game's
  tiles / matching_first: every one of them shown, the game's tiles in the places left / all / custom_only),
  **`custom_tile_choices`** (the tiles you want for sure), **`custom_tile_weights`**. A game brings its drawing in
  when its name holds the drawing's game, whatever the capitals (`Super Mario 64`, `TUNIC`, a Manual's
  `Manual_SuperMarioBros3_...`); pelly, Archipelago's own, is in every multiworld.
  **`custom_drawings`** for the drawing panels' scribbles, none yet. How to propose a tile:
  `mod/assets/tiles/README.md`.
- **`pack_sanity: junk`** and **`flare_gun_sanity: junk`**: the same checks, holding only filler or a trap.
- **`pack_sanity: vanilla`**: each pack's check gives the pack itself, which stays in your hands; no other location
  gives one.
- **`linear_towers: one_deposit_box`**: a tower's key comes once its monument holds a number of gourds drawn for each
  tower, at a check of its own, the "Monument Key".

### Fixed

- The peg tiles a puzzle part hid could fall off their racks when their item came.
- The seed's colors kept the order they were drawn in (the flare guns took the lowest ones).
- `junk` options leave the progression enough room: a tight seed could fail to fill.

## 0.7.1

### Moving from 0.7.0 to 0.7.1

- **Everyone needs this build:** the host's channel to its guests changed (protocol v21).
- **A 0.7.0 YAML generates almost as it did**, with two changes when `object_sanity` or `light_sanity` is on: their
  checks now hold junk for 75% of them with `*_items: any` (`object_sanity_junk_percentage`,
  `light_sanity_junk_percentage`; set them to 0 for the 0.7.0 behaviour), and a big pool fills with the island's
  gadgets past `island_object_limits` instead of bonuses.
- A seed with only a `goal` now shows it as its one objective, on the overlay and on the hub's board.

### Added

- **`zone_locks`**: with Object or Light Sanity, the island's lamps and gadgets locked by the zone of the nearest
  coloured tower, eight items (`Red Zone Lamps`, `Red Zone Gadgets`, and the same for green, blue and yellow). A
  zone's gadgets are off the map until its item arrives; its lamps and light switches stay usable but are out of logic
  until then. **`start_with_zone_locks`** and **`zone_locks_local`**. The tutorial's, the Map Room's, the
  chairlift's, the tunnels' and the train's keep their lock.
- **`object_sanity_junk_percentage`** and **`light_sanity_junk_percentage`** (75): the share of those checks that
  holds only junk with `*_items: any`.
- **Big Brother** pitches the voices up while everyone is small.

### Fixed

- `junk` for Object or Light Sanity could not be kept in a big pool (the filler past the gadgets' limits was all
  bonuses, which are useful items).
- The YAML template described only the first traps; it describes all 24.
- The rare FillError with `linear_keys` and `linear_towers` together is rarer.

## 0.6.0 (unreleased)

### Moving from 0.5 to 0.6

- **A 0.5 YAML generates almost as it did:** the new options are off by default, except two:
  `monument_sanity` is on (7 more checks, the monuments full; set it off for the 0.5 count) and
  `lock_endings_until_objectives` is on (with objectives, the ending waits for the others; set it off for the 0.5
  behaviour).
- **Everyone needs this build:** the host's channel to its guests changed (protocol v18).
- A 0.5 seed keeps working with the new mod; the new options need a new seed.
- The joke filler items (Boid, Plumbus, Bouba...) are gone, so those names are free for `gourd_name`.
- **Removed:** the hidden `lock_arch_doors` of 0.1.1 (use `start_with_arch_doors_open`), and `big_walk` / `big_bell`
  in `objectives`, which were only accepted to be refused.

### Added

- **`lock_abilities`** (`handed` / `ambidextrous`): the players' bodies are items, received by every player at once.
  **Legs** (jumping and crouching, needed to leave the starting zone), **Seat** (a chair, the floor), **Progressive
  Arm** (raising, then pointing, then extending sideways; one per arm with `handed`, both arms with `ambidextrous`),
  **Throwing** and **Carrying** another player. **`start_with_abilities`** lists those you already have (legs, seat, arms, throwing, carrying; none by
  default), as `start_with_puzzle_needs` does for the puzzles' parts. **`lock_running`** locks running too (useful, never required). The
  gesture puzzles ask for what they use (Concert, Indoor Semaphore, Charades, Pointer's Paradise...), and Pointer's
  Paradise also asks for Carrying. Big Throw still throws while Throwing is locked.
- **`object_sanity`** (`first_pickup` / `every`): the island's gadgets stay on the map, and picking them up sends
  checks, the first of each kind or each one (an object then leaves the map, like a pack). **`object_sanity_kinds`**
  picks the kinds (walkie-talkie, radio, megaphone, binoculars, laser, torch, scanner, clock, compass, map, cowbell,
  lamp, paintbrush; the paintbrushes are off by default and never leave the island); a kind left out leaves the island.
  **`object_sanity_items`** (`any` / `local` / `junk`) chooses what those checks may hold.
- **`light_sanity`** (`lamps` / `lamps_and_switches`): a check the first time each light is switched on, the island's
  lamps and, if you like, the wall switches of the Doof Chamber, the salons, the tutorial, the train and its stops.
  With it on, a lamp is a light, not an object. **`light_sanity_items`** (`any` / `local` / `junk`) chooses what those
  checks may hold.
- **`monument_sanity`** (on by default): a check when each monument is full. **`linear_towers`** puts each tower's key there, as in the
  game.
- **The vanilla behaviours back, as options:** **`linear_keys`** (a placed key opens its door), **`linear_puzzles`**
  (each puzzle holds a gourd, the seven purple ones aside), **`start_with_map_room_open`**, **`include_big_game_puzzles`** (`from_start` /
  `when_unlocked` / `disabled`) and **`cut_key_sanity`** (`on` / `junk` / `off`).
- **Three objectives, each at a share you choose:** **`big_lights`** (`lights_required` %; without `light_sanity` the
  lamps count for it, with no check), **`big_objects`** (`objects_required` %) and **`big_body`** (`body_required` %,
  Running aside).
- The options are regrouped: **Vanilla Behaviours** and **Movement Locking** are groups of their own, every sanity
  is under Sanity, and `fast_golf` is under Puzzle QoL.
- **Three traps:** **Big Silence** (every player muted for a while), **Big Slide** (the ground turns slippery) and
  **Big Time** (a clock in every player's hands, on the real time of their own computer).
- **`fast_golf`**: the golf ball is no longer too heavy.
- **A short line on the screen for every check sent:** what it was, what it gave and to whom.

### Changed

- **`lock_endings_until_objectives` is on by default:** with objectives, the ending stays shut until the others are
  done. A seed without objectives is not touched.
- The lamps on the red pedestals light up in orange, the colour their sphere always has (the game never lets it change).
- With `lock_abilities` and no room left in the starting zone for Legs (the tutorial's puzzles holding their gourds
  and its monument waiting for an item), the player starts with Legs, and the generation log says so.

### Fixed

- Two lights counted as switched on as the world loaded (the salons' main lights): only what players do counts now.
- With `include_big_game_puzzles` other than `from_start`, the purple gourds could still show on the map.

## 0.5.0 (unreleased)

### Moving from 0.4 to 0.5

- **A 0.4 YAML generates as it did.** `objectives` is empty by default, which keeps the single `goal`, and `tower_mode`
  is `all_open`. One default changes the seed: `tower_sanity` adds seven checks (the towers reached), so a seed made now
  has 7 more locations. Turn it off for the 0.4 count.
- **Everyone needs this build:** the host's channel to its guests changed (protocol v15).
- A 0.4 seed keeps working with the new mod; the new options need a new seed.
- **`goal: big_wall` with `gauntlet_mode: locked_stages` is refused**: the chapel bell ends the run before the Gauntlet.

### Added

- **`objectives`**: things to do besides the `goal`, in any order: `big_collection`, `big_keys`, `big_radio`, `big_towers`,
  `big_climb` (the highest point), `big_fiesta` (the party on the beach: a radio plugged into the big speakers, with a
  music playing; also read as `big_club`). The `goal` is then the run's ending: `big_wall` (the chapel bell, now a goal
  for a short seed without the Gauntlet), `big_goodbye` or `big_game`. **`random_objectives`** draws a number of them,
  from the list or from all. They are on a board in the hub, ticked as they are done;
  **Settings > Archipelago > Objectives on screen** keeps them on the overlay. `keys_required`,
  `radio_stations_required` and `towers_required` set the counts. `lock_endings_until_objectives` keeps the Gauntlet's
  last stairway and the secret room's printer shut until the other objectives are done.
- **`tower_mode`**: `all_open` (as before), `in_order` (the monuments take gourds one at a time, in an order the seed
  draws; a monument that is not the next refuses a gourd and its tower's button stays still) and `by_item` (the same,
  and each monument after the first also needs a **Progressive Monument**). The logic is exact: nothing is spent in
  a way it did not count.
- **`first_tower_open`** (on by default): with `by_item`, off means no monument is open at the start, the first waits for a
  Progressive Monument too (seven in the pool instead of six). Needs `by_item`.
- **`everyone_gathers`** (on by default): `big_climb` and `big_fiesta` count their ten seconds only while every player is at the place.
- **`tower_sanity`** (on by default): a check for reaching each of the seven towers, made when its opening button is
  pressed (the drawbridge and the Green Dome: when a player gets there). **`hint_discovered_key_locations`**: hints, at that moment, what sits at the tower's key cuts and its key
  deposit. **`hint_discovered_deposit_boxes`** (with `tower_mode`): the gourd deposits in its monument.
- **The board does not give the order away:** only the towers already full and the one open are named; `ShowTowerOrder`
  in the .cfg lists them all. The overlay says which tower is open (`Tower open: Yellow Twist Tower (2/5)`) or that a
  Progressive Monument is missing.
- **Two small sounds**: a cowbell when an item arrives, a plinth when a check other than a puzzle is sent, one per burst
  of fifteen seconds. **Settings > Archipelago > Archipelago alerts** (text and sound, sound only, text only, off).
- **Lines on the screen for what happened:** DeathLink sent, received and forgiven, a Big Trip with nowhere to go, the
  Settings overriding the seed, a key placed before its item.
- **`gourd_name`** (letters, digits, spaces and a few signs; the rest is dropped) renames the gourds for the run, on the server, in the Text Client, in other games, in the
  trackers, in the spoiler and on the overlay.

### Changed

- **Everything the host hears, the guests hear:** the received and check sounds, the confetti of an objective done, the
  DeathLink and trap cowbell (the host's snapshot carries a count of each).
- The overlay keeps "Archipelago: Connected" and what arrives; the puzzle parts missing are no longer listed.
- **X-Ray Goggles** are called **Scanner** (same item id; `island_object_limits` still reads the old name).
- The Eggs need is shown as **Terrain Pegs** (the YAML key stays `eggs`).
- **DeathLink** and its amnesty are one row in Settings > Archipelago.
- **The joke filler items are gone** (Baby, Boid, Plumbus... and the hidden `joke_filler_percentage`): their names are
  free for `gourd_name`.

### Fixed

- **A lamp keeps one colour:** switched on, an island lamp went back to its own body colour while its light kept the
  seed's; the body, the halo and the light now keep the seed's colour.
- **Big Trip and Big Meeting spare the Gauntlet again.** Its box ran from the world's origin to the Gauntlet (particle
  renderers), so every place was dropped and every player spared; the box is now the Gauntlet's own.

## 0.4.0 (unreleased)

### Moving from 0.3 to 0.4

- **One thing stops a 0.3 YAML from generating.** `cabin_fever_time`, `cabin_fever_long_time` and
  their `_seconds_min` / `_seconds_max` are gone: use `cabin_fever_seconds` and
  `cabin_fever_long_seconds` (300 and 1800, the game's own waits). For a wait drawn once per seed,
  Archipelago's own syntax: `cabin_fever_seconds: random-range-60-300`. Everything else in a 0.3
  YAML works.
- **The defaults make a bigger seed.** 117 locations instead of 93 (12 packs, 8 fireworks, 4 flare
  guns), a quarter of the filler as bonuses, and colors drawn per seed. `pack_sanity`,
  `firework_sanity`, `flare_gun_sanity`, `bonus_fill_percentage` and `random_colors` turn them off.
- **Everyone needs this build:** the host's channel to its guests changed (protocol v12).
- **Some settings are not in the YAML:** they are in the game, Settings > Archipelago (below).

### Added

**Checks and items**

- **24 more checks, on by default.** `pack_sanity`: the island's 6 backpacks, 5 belts and gourd
  carton stay on the map, and picking one up for the first time is a check (named by color: Sky Blue
  Backpack, Purple Belt, ...); the pack then disappears. Off, they are on the map as in the game, free,
  with no check. `firework_sanity`: firing each of the 8 firework launchers. `flare_gun_sanity`:
  picking up each of the island's 4 flare guns, which then come back as items too (off, they stay
  on the island and none is an item).
- **The Rainbow Flare Gun:** a white flare gun whose shots flicker through every color, in the pool
  once in every seed.
- **`island_object_limits`**: the most of each island object the pool holds (2 buoy lights, 2 maps,
  8 walkie-talkies, 3 of the rest, 1 of each flare gun and of the Rainbow by default; 0 leaves one
  out). Past every limit, the rest of the filler is bonuses. The flare guns are not random filler:
  their limit is how many the pool holds, with `flare_gun_sanity` (the Rainbow always).
- **`backpacks_in_pool`, `belts_in_pool`, `gourd_cartons_in_pool`** (2, 2 and 1 by default; up to 8,
  8 and 4), in a new Item Pool group: how many of each the pool holds, taken out of the filler.

**Colors**

- **`random_colors`** (on by default): five to ten colors drawn per seed paint the island's buoys
  (body, halo and light) and its flare guns (body, barrel and shot: flare, light and smoke), the same
  on every screen. **Only objects of the map:** never the players, the gourds or the keys. The item
  feed names a flare gun by its color ("Received: Pink Flare Gun").
- **`gourd_name`**: renames the gourds for the run (24 characters max) in the server, the Text
  Client, other games, Universal Tracker, the spoiler and the overlay. `!hint Gourd` still works.
  One name per multiworld: if Big Walk slots disagree, or the name is already taken, it stays "Gourd".

**Traps and bonuses**

- **Traps** (`trap_fill_percentage`, 0 by default, `trap_weights`, `hard_traps`, `trap_duration`, 20 s
  by default, `traps_spare_the_gauntlet`), each one on every player of the session: Big Drop
  (everyone drops what they hold), Big Throw (hard: a full wind-up that cannot be cancelled, then a
  throw), Big Trip (hard: everyone sent to a different place already reached), Big Meeting (hard:
  everyone gathered around one player), Big Night (the clock fast-forwards to midnight, the same
  hour for everyone, and runs on from there), Big Sleep (hard: for `trap_duration`, everyone falls
  asleep for two seconds out of seven), Big Load (hard: every pack worn comes off and what it holds
  falls out), Big Mask (hard: for `trap_duration`, everyone wears the blindfold puzzles' helmet and
  nobody can take it off; a player already masked is spared), Big Flare (a flare gun shot lands on
  every player, in the Archipelago colors).
- **Bonuses** (`bonus_fill_percentage`, 25 % of the filler by default; `bonus_weights`, 0 leaves one
  out): Big Speed, Big Jump, Big Day (a fast forward to noon).
- **`trap_link`** (off by default): TrapLink. The traps of the pool that reach you are sent to the
  other games that have it on, and their traps play here: as the Big Walk trap of the same name or
  the nearest by its name (a freeze is Big Sleep, a bomb is Big Flare), and drawn from the DeathLink
  roulette when none comes near. Bonuses are never sent. A trap that comes from a DeathLink or from
  a TrapLink is not sent on, so none loops.

**DeathLink**

- **`death_link`** (off, send, receive or both). Sent when a puzzle is failed
  (`death_link_triggers: [puzzle_failed]`): a validator screen going red, buttons pressed out of
  order, a synchronised button letting go unsolved, a dispenser's timed tomato running out, 29
  puzzles in all; add `big_fall` for a fall that leaves a player dazed. `death_link_amnesty`
  forgives that many first. The message reads "*slot* failed the *puzzle* (*how*)".
- **Received** (`death_link_effect`): by default a **knock-out**, everyone drops what they hold, takes
  off their backpacks and belts (they spill) and is dazed as after a big fall, which sends nothing
  back; or `drop` only; or a `roulette` of traps (`death_link_trap_weights`,
  `death_link_roulette_hard_traps`).
- **`death_link_target: one_player`**: a DeathLink received hits only the player hit the fewest times
  so far this session. With the roulette, Big Meeting and Big Night, which need everyone, are left
  out of the draw.
- **`death_link_trap_on_send`**: with `send` or `both`, when this world sends a DeathLink its own
  players take the effect of a death too, as the others do.

**In the game: Settings > Archipelago**

A new category, last in the game's Settings (main menu or pause menu), kept in the mod's .cfg on the
machine that sets it: the overlay's **text size** (each player); **DeathLink** on or off and its
**amnesty** (the host, over the YAML for the session); **Gentle effects** (each player: the traps'
flashes, Big Flare's today, are not shown on their screen; off by default); and the **gourd name**
(the host: free text, applied on Enter or on leaving the field, shown on every player's overlay). A
guest sees the host's, greyed. Left at "Gourd", it says the seed's `gourd_name`; anything else typed
there wins over it on the overlay only, the server and the other games keep the seed's.

**A warning when the builds do not match**

- The host sees a red line for each player on another build of the mod, or who has not said hello
  after a minute (no mod, or an old one). A guest whose host runs another build sees one too,
  where its screen used to stay empty. Everyone still needs the same build.

**Tools**

- Up to three loopback guests on one PC (Ctrl+L again for each, four players), Ctrl+T cycles
  through every window; `tools/deathlink.py` plays another game (DeathLink, TrapLink, server
  commands).

### Changed

- **Cabin Fever in one number per puzzle** (players), `cabin_fever_seconds` and
  `cabin_fever_long_seconds`; see above for the YAMLs it breaks.

## 0.3.0 (2026-10-05)

Numbered 0.3.0 rather than 0.1.3: a release this size moves the minor number; the next ones are 0.4, 0.5...

### Changed

- **Options sorted into five groups**: Goal, Sanity, Logic, QoL, Puzzle QoL.
- **`lock_puzzle_needs` is on by default**: the parts puzzles are built from are items to find.
- Puzzle needs checked against what the mod really hides in each puzzle's place: the validators'
  buttons for Fielding, both Obbies, Blindfold Catwalk, Optical Telegraph and Indoor Semaphore, the
  Timed Tomato for the Obby, icon panels for Indoor Semaphore, pose panels for the Charades Rooms.
  A seed made before this may put one of them in logic a little early.
- With `lock_puzzle_needs`, Buttons, Synchronized Buttons and Icon Panels are asked for early
  (in any world), so that a solo seed with the defaults always generates (1 in 23 failed).
- **Renamed**: `gourd_slot_checks` is now `gourd_sanity`, `radio_checks` is now `radio_sanity`.
  A YAML with the old names falls back to the defaults: rename them.
- **The towers' keys and checks carry the names the players use**: **Red
  Funnel Tower Key**, **Green Cup Tower Key**, **Blue Castle Tower Key**, **Yellow Twist
  Tower Key**, **Black Monolith Tower Key** and **Green Dome Tower Key**, with their
  cuts and deposits (**Red Funnel Tower Key Cut 1**, **Red Funnel Tower Key Deposit**).
  Ids did not move, so a seed made by 0.1.2 still plays, but a YAML or a plando that
  names one of these locations or items needs the new name.
- The channel between the host and its guests is now version 8: everyone
  needs 0.3.0.
- Ctrl+R is gone: the resync stations do it.

### New

- **The Silent Gauntlet** (`gauntlet_mode`, `vanilla` by default). With
  `locked_stages`, each of the seven stages is a check (solving its puzzle)
  and its way up is an item: **Gauntlet Stage 1 Door** to **Stage 7 Door**.
  The buttons that opened the stairways when everyone held them are gone, and a
  stage's wall and stairway stay shut until its item arrives. The `big_goodbye` goal needs all
  seven.
  - `gauntlet_puzzles_required` (on by default): on, the puzzle is still needed
    to open the wall inside the stage, which then also waits for the stage's
    item. Off, the item opens the wall at once, so the puzzles can be done in
    any order, or skipped; they stay checks.
  - `gauntlet_stage_items` (`progressive` by default): seven **Progressive Gauntlet Doors**, each
    opening the next stage up; `individual` keeps one item per stage, found in any order.
  - `gauntlet_stages_local` (on by default): keeps the seven items in your own
    world.
  - `lock_gauntlet_needs` (on by default, in `vanilla` too): with
    `lock_puzzle_needs`, the `big_goodbye` goal, and with `locked_stages` each
    stage's check, also need the parts its puzzle
    is built from, and the mod hides those parts in the seven stages until
    their item arrives. The finale and the entrance are left alone.
- **`require_arch_doors`** (on by default): what lies past the Left and Right Arch
  Doors is behind them in logic, so that nobody walks to a place and is sent its
  shortcut afterwards. Left: the Yellow, Blue and Black towers and the Green
  Dome; never the chapel, so the end of the game needs only the Big Wall Door. Right: the Green Tower and what is past the chairlift. Told by the
  player; a list too short asks for fewer doors, never more than a seed holds. The
  two doors are also asked for early in your own world, so they are easy to find,
  while the First Arch Door is open and `lock_puzzle_needs` is off; otherwise the
  first locations are too few for them and generation can fail.
- **`hint_keys: from_start`**: the seven big keys are hinted from the first
  minute.
- **Teleport buttons** (`teleport_buttons`, `off` by default). Buttons in the world, set into
  the walls: in the hub one for each of the Red Funnel, Green Cup, Blue Castle, Yellow Twist and
  Black towers and the Silent Gauntlet, and one back to the hub inside each of them. The
  Green Dome has none, being next to the spawn. `free`: all there from the start.
  `with_towers`: a tower's button once its door has been opened by the button at its foot (the
  Black Tower's once the two buttons at its top have been held together, the Gauntlet's once the chapel has been opened). `items`: the same, and each also
  needs its own **Teleporter** item, six of them, useful and never required. A teleport can skip a lock the
  logic does not count on, so none of this is logic. Guests can press them too. `teleport_back_to_hub` (off by default) adds a way back in each destination.
- **`tile_thief`** (`vanilla` by default): a button beside Tile Thief that makes new peg tiles in
  front of it, for every player: `easy` the ones the puzzle expects, `chaos` one of every kind it
  draws from. With `lock_puzzle_needs` it waits for the puzzle's panels.
- **Peg tiles shuffled**: in every Archipelago game, the island's peg tiles swap places on their
  stands, within each place, differently for each seed. Not an option; a save hosted without
  Archipelago keeps the game's layout.
- **`open_black_tower`** (on by default): the Black Tower's door at its foot is open from the
  start, instead of waiting for the monuments of the hub and the four towers to be full.
- **Cabin Fever waits** (`cabin_fever_time`, `cabin_fever_long_time`: `vanilla` by default,
  `reduced`, `random_between`, `fixed`, with `_seconds_min`, `_seconds_max` and `_seconds`), each
  with a hidden help button (`cabin_fever_help`, `cabin_fever_long_help`) that takes ten seconds
  off a running wait.
- **Resync stations replace Ctrl+R**: in the hub and in each of the five towers, a button that
  takes back every gourd, key and gadget of yours that is lying around and puts them in front of
  itself, a toggle beside it to leave the gadgets out, and a sign saying what the button does.
  Host only unless `guests_can_resync` (off by default), and one resync at a time on the whole
  map. `tower_resync_stations` (off by default) adds the five towers' to the hub's.
- **A new save gets its deposits back.** The number of gourds placed in the
  monuments is kept on the Archipelago server, per slot, and only ever goes up.
  A new save on the same slot puts that many back as the monuments load, in
  whichever slots come first, instead of leaving them to be carried over by
  hand. If the server does not answer, nothing is restored and nothing breaks.

### Fixed

- Universal Tracker rebuilds a seed with its `gauntlet_stage_items` and `teleport_buttons`, and a
  seed made before progressive Gauntlet doors with one door per stage (it was never in go mode).
- A save connected to a different seed or slot than it knew no longer sends the checks
  it reported to the previous one. Its first connection still sends the ones made
  while offline.
- A big key already put in its plinth stays there when its item arrives later (it was taken
  out to be handed over, and the drawbridge went back up at the end of a run).
- A big key no longer flies out of its stone when its monument fills before its
  item has arrived (the drawbridge's, then the others); it stays in place until
  the item comes. Found in play, twice.
- With `lock_puzzle_needs`, the "skip this challenge" panels (the game's own
  accessibility setting) are no longer hidden with the buttons: each shows once the
  parts of its own puzzle have arrived.

### Dropped

- "All Radio Stations" as a check (it is an objective, for 0.1.5) and "<Tower>:
  All Gourds Deposited" (gourds go in any tower in any order, so it cannot be
  kept in logic).

## 0.1.2

### New

- **Puzzle parts** (`lock_puzzle_needs`, off by default). What a puzzle is
  built from becomes an item: Buttons, Synchronized Buttons, Icon, Drawing,
  Pose, Sound and Point Panels, Speakers, Lights, Teapots, Timed Tomato, Golf
  Ball, Big Head Mask, Ink Viewer, Counter, Coordinates Computer and Terrain Pegs
  (17). A part is not on the map until its item arrives, and then appears for
  every player. Nothing blocks a puzzle itself, and the generator counts on a
  puzzle only once it holds every part it needs.
  - `start_with_random_puzzle_need` (on by default): one random part at the
    start, so a tutorial puzzle is always doable. `start_with_puzzle_needs`
    lists parts to start with. Turning the random one off without listing a
    part a tutorial puzzle can use stops generation with a clear message.
  - Left alone on purpose: door handles, the train's controls, the hub gates,
    the lookouts, the radio stations, the Silent Gauntlet and the end of the
    game.
  - A line in the corner says which parts are still missing, for the host and
    its guests.
- **`start_with_arch_doors_open`**: any set of the hub's three arch doors can
  be held closed until their item arrives, the First Arch Door alone included.
  Replaces `lock_arch_doors`, which is still read from old YAMLs and seeds.
- **Big keys arriving during play go into somebody's hands**, like a gourd
  does, instead of landing two metres ahead of whoever the mod picked.
- **A session journal**, `BepInEx/session-journal.tsv`: one line per event
  (connection, items received and from whom, checks sent, parts opened, world
  loaded or left) with the time and where the player stood. It is only ever
  added to, rotates at 4 MB and survives restarts, unlike `LogOutput.log`.
- Debug keys (with `Debug.Enabled`): Ctrl+E names what is under the crosshair,
  Ctrl+Z logs the state of every part, Ctrl+Q and Ctrl+W lock and unlock parts
  by hand, Ctrl+D fixes the time at noon, Ctrl+P sends a DeathLink.

### Changed

- **The towers' keys and checks carry the names the players use**: **Red
  Funnel Tower Key**, **Green Cup Tower Key**, **Blue Castle Tower Key**, **Yellow Twist
  Tower Key**, **Black Monolith Tower Key** and **Green Dome Tower Key**, with their
  cuts and deposits (**Red Funnel Tower Key Cut 1**, **Red Funnel Tower Key Deposit**).
  Ids did not move, so a seed made by 0.1.2 still plays, but a YAML or a plando that
  names one of these locations or items needs the new name.
- The co-op channel between a host and its guests is protocol v2. **A guest on
  another build ignores its host.** Everyone in a session needs the same
  build.

### Removed

- `lock_puzzles` (a per-puzzle Unlock item), which was only ever in
  development builds: no puzzle sits behind an item of its own any more, only
  behind the parts it is built from (`lock_puzzle_needs`). A YAML that still
  sets it is only warned about.

### Fixed

- The `.apworld` lacked `version` and `compatible_version` in its manifest,
  which Archipelago 0.6.7 requires: it loaded the world as version 0.0.0 and
  refused any YAML with a `requires` line. `apworld/build.py` now writes them
  into the package.

## 0.1.1

The first alpha's feedback: sealed-box pick-ups, belts, gourd colours, items
going to every player in turn, arch doors as items, co-op testable on one PC
with a second local instance (Ctrl+L). Test 24 of `docs/COOP-TESTS.md` (a
puzzle gourd stowed in a bag) was built and never run.

## 0.1.0

The first alpha.
