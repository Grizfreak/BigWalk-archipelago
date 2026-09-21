using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Stops a big key from opening its own door, and nothing else.
    //
    // This is the ONE thing the "big keys as forage checks" design has to
    // suppress. Cutting a key and placing it in its plinth are both left
    // entirely alone — the players do both freely and both are checks, the
    // same shape as a puzzle whose gourd is a check. Only the door has to
    // stop following, so that the feature item means something (see
    // Core/KeyFeatures.cs and ../../../apworld/design-decisions.md).
    //
    // WHAT IS BEING SUPPRESSED. `Prop.SetPinDirectControlSystem(PropHome
    // home, bool pinned)`, decompiled rather than guessed, drives three
    // TrackedPeckStates when a prop is pinned:
    //
    //   1. `home.pinDirectControlSystem`   — **THE DOOR**
    //   2. `this.pinDirectControlSystem`   — null on every big key
    //   3. every `this.taggedPinSystems[i]` whose `propGroup` equals
    //      `home.pinGroup` — empty on every big key
    //
    // (2) and (3) were measured empty in game on 2026-09-21, on all seven
    // keys, which refuted a first reading of this function that had put the
    // feature in (3). (1) was then driven directly with `SetState(1)` and the
    // tutorial drawbridge opened with no key in its plinth. So the door is
    // the HOME's state after all — an unlabelled, `NotSavable`
    // TrackedPeckState on a GameObject called `BigKeyPlinthNetworking`, one
    // per plinth, sitting inside the feature it opens
    // (`ChairLift/Poles/…`, `TrainSystem/ActivatorObjects/…`,
    // `TunnelSystem/TunnelOutlet - Yellow/…`).
    //
    // NULL IS SAFE HERE, and that is not an accident of style — it is read
    // off the disassembly. Branch (1) guards its own read
    // (`if (state != null && state.m_CachedPtr != null)`), so blanking the
    // field for the duration of the call makes the game skip it cleanly.
    // Branch (3) has no such guard, which is why the earlier version of this
    // patch had to substitute an EMPTY array rather than null. Different
    // branches, different rules; do not generalise one to the other.
    //
    // Prefix/postfix rather than a transpiler: IL2CPP has no IL to rewrite,
    // and a swap either side of the call is both readable and exact.
    //
    // No host check. The original is already server-side — its very first
    // branch logs a warning and returns when it is not — so a guest never
    // reaches the driver, and the door they see is the one the host's peck
    // state replicated to them. That is also why this needs no co-op
    // counterpart, unlike the radio: peck state is networked, `FmRadioManager`
    // is not.
    [HarmonyPatch(typeof(Prop), nameof(Prop.SetPinDirectControlSystem))]
    internal static class PropPinDoorPatch
    {
        // Arguments by INDEX, not by name. The target is private, which is
        // fine — Il2CppInterop re-emits the game's private methods as public,
        // so `nameof` resolves and the compiler checks it — but parameter
        // names survive into the interop assembly only by convention, and a
        // prefix asking for one that is not there throws at `PatchAll` and
        // takes the whole plugin down. `__0` is `PropHome propHome`, `__1` is
        // `bool pinned`.
        private static void Prefix(Prop __instance, PropHome __0, bool __1,
            out TrackedPeckState __state)
        {
            __state = null;

            if (!ShouldSuppress(__instance, propHome: __0, pinned: __1))
                return;

            __state = __0.pinDirectControlSystem;
            __0.pinDirectControlSystem = null;

            // Said out loud, and it is not only for the bring-up. Suppression
            // is invisible by construction: what it produces is a door that
            // does not move, which looks exactly like a key placed in the
            // wrong plinth, a patch that failed to bind, or a feature already
            // granted. One line turns all four apart.
            Plugin.Log.LogInfo(
                $"[{nameof(PropPinDoorPatch)}] {__instance.saveablePropName} placed in "
                + $"{__0.saveableHomeName}: its door was held back"
                + (KeyFeatures.IsGranted(__instance.saveablePropName)
                    ? " (already granted, so it is open anyway)."
                    : " — it opens when the matching item arrives."));
        }

        private static void Postfix(PropHome __0, TrackedPeckState __state)
        {
            // Put back unconditionally when it was taken. The field belongs to
            // a scene object that outlives this call, and a plinth left
            // blanked would never open even once the item arrived —
            // Core/KeyFeatures.FindDoor reads this very field.
            if (__state != null)
                __0.pinDirectControlSystem = __state;
        }

        private static bool ShouldSuppress(Prop prop, PropHome propHome, bool pinned)
        {
            // While the features are not Archipelago items the game is left
            // completely alone. That covers an apworld too old to send the
            // flag, a seed with the option off, and a session with
            // Archipelago disabled entirely — in all three, a key opening its
            // own door is the correct behaviour and the only one that is
            // playable.
            if (!KeyFeatures.FeaturesInPlay)
                return false;

            // Unpinning drives the same state with `pinned` false. Letting it
            // through costs nothing — there is no feature to close, because
            // there was none to open — and it keeps the suppression to the
            // single transition it is about.
            if (!pinned)
                return false;

            if (prop == null || propHome == null)
                return false;

            // BOTH halves, and the second one was missing at first. A big key
            // has two homes in play: the plinth it is carried to, and the
            // `notSavable` one it sits in at the foot of its tower — which
            // `Prop.Start()` pins it into on every world load. Keying on the
            // prop alone therefore fired this seven times per load, blanking a
            // state that is not a door and reporting a suppression that never
            // mattered (observed in the log on 2026-09-21).
            return GourdRegistry.IsBigKey(prop.saveablePropName)
                   && GourdRegistry.IsBigKeyPlinth(propHome.saveableHomeName);
        }
    }
}
