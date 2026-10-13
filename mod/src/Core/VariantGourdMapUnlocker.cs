using System;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Automatically reveals "variant challenge" gourds (purple/postgame) on
    // the map, continuously — not a one-time-per-save thing like
    // Core/ArchDoorUnlocker.cs: GourdMap.Initialize() systematically resets
    // the icon to Hidden on EVERY new GourdMap instance (i.e. on every zone
    // change), cf. VariantGourdRevealer. Polled at an interval rather than
    // every frame (FindObjectsByType has a cost); GourdFlag.SetState has an
    // early-return no-op if the state doesn't change, so calling it again
    // on an already-revealed gourd does nothing. Purely a local/map display
    // state (no SaveManager, no network): no need for a NetworkServer.active
    // guard, runs independently on every client.
    internal class VariantGourdMapUnlocker : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public VariantGourdMapUnlocker(IntPtr ptr) : base(ptr)
        {
        }

        private const float PollIntervalSeconds = 2f;
        private float _nextPollTime;

        private void Update()
        {
            if (!WorldManager.isReadyForEffects || Time.time < _nextPollTime)
                return;

            _nextPollTime = Time.time + PollIntervalSeconds;

            // L4 (0.6): with `include_big_game_puzzles` when_unlocked or disabled, the game's own way: hidden until an
            // ending. The host waits for its slot before revealing anything: a reveal made before the slot arrived
            // stayed on the map (seen 2026-10-13). A guest has no slot and keeps revealing (it only shows more), and so
            // does a game with Archipelago switched off.
            var isGuest = Mirror.NetworkClient.active && !Mirror.NetworkServer.active;
            var slot = Net.ApRuntime.SlotData;
            if (!isGuest && ModConfig.ArchipelagoEnabled.Value && (slot == null || slot.IncludeBigGamePuzzles != "from_start"))
                return;
            VariantGourdRevealer.RevealAll();
        }
    }
}
