using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Ouvre automatiquement les "Arch doors"/HubGate (raccourcis du hub,
    // normalement débloqués un par un via un bouton à sens unique derrière
    // chaque porte) dès la première session sur une save donnée. Décision
    // utilisateur (cf. big-walk-archipelago-notes.md, session du 2026-09-09) :
    // ces raccourcis n'ont pas d'intérêt à rester fermés pour un monde
    // Archipelago, contrairement aux gourds/big keys qui sont eux le vrai
    // contenu randomisé.
    //
    // Fix timing : polling simple dans Update() sur WorldManager.
    // isReadyForEffects (propriété statique du jeu, conçue pour signaler "sûr
    // de déclencher des effets maintenant") — un event ponctuel
    // (OnWorldManagerStart, onLocalPlayerCharcterStart) se déclenchait trop
    // tôt, avant que le "peck manager" du jeu existe (confirmé en jeu par une
    // rafale de "no peck manager instance. this is maybe too early").
    // Composant ajouté inconditionnellement (pas seulement si Debug.Enabled,
    // cf. Plugin.Load()) : ce n'est pas un outil de debug, une vraie
    // fonctionnalité du mod.
    //
    // Ciblage précis (2026-09-09) : PeckDevHelper.Trigger(UnlockRules{unlocks
    // =true}) — le cheat dev déjà intégré au jeu trouvé en scannant les
    // composants près d'un HubGate — ouvrait bien les Arch doors, mais
    // broadcastait à TOUTE la catégorie "unlocks" : inspection directe de
    // plusieurs fichiers de save a confirmé qu'il persiste aussi EndingGate=1
    // et les 7 FmStation*/4 LookoutLight* à 1, en plus des 3 clés hub qu'on
    // veut vraiment. Décompilation Ghidra de TrackedPeckState.SetState a
    // confirmé que la vraie clé SaveManager écrite est Enum.ToString(
    // savableSystem) — donc écriture directe ici, sans passer par Trigger, ce
    // qui évite tout effet de bord sur EndingGate/FmStations/LookoutLights.
    // Les 3 clés ci-dessous sont les seules de catégorie "hub shortcut"
    // confirmées écrites ensemble par Trigger(unlocks) sur plusieurs saves de
    // test (SpawnHubGate + HubTunnel + HubShortcutToSportsCreek) — même
    // philosophie que les Arch doors (raccourcis de navigation du hub), donc
    // gardées ensemble ici.
    //
    // Effet en direct (2026-09-09) : une écriture SaveManager brute seule ne
    // provoque aucun effet visuel dans la session en cours (confirmé en jeu :
    // portes toujours fermées après écriture directe, sans reload) — contrairement
    // à Trigger() qui passait par la vraie mécanique Peck. Fix : appeler
    // TrackedPeckState.SetState(1) directement sur les instances trouvées en
    // scène (savableSystem == la clé visée) plutôt que SaveManager.SetIntValue
    // — SetState fait exactement la même écriture en interne (confirmé par
    // Ghidra) tout en déclenchant les callbacks visuels/d'animation, sans le
    // broadcast large de Trigger(). Fallback sur l'écriture brute si l'objet
    // n'est pas chargé dans la zone actuelle (même philosophie que
    // ItemApplier.TryApplyLiveEffect pour les gourds/big keys) : la
    // restauration au chargement prendra le relais au prochain reload.
    internal class ArchDoorUnlocker : MonoBehaviour
    {
        // Constructeur requis par Il2CppInterop pour tout type injecté en IL2CPP.
        public ArchDoorUnlocker(IntPtr ptr) : base(ptr)
        {
        }

        private const string OpenedFlagKey = "ap_archdoors_opened";

        private static readonly SavableSystem[] HubShortcutKeys =
        {
            SavableSystem.SpawnHubGate,
            SavableSystem.HubTunnel,
            SavableSystem.HubShortcutToSportsCreek,
        };

        private bool _done;

        private void Update()
        {
            if (_done)
                return;

            // Modèle d'autorité host, comme le reste du mod (cf. ItemApplier).
            if (!NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            // Fixé dès la première frame où les deux conditions sont réunies,
            // que le flag SaveManager soit déjà posé ou non — évite de
            // re-vérifier à chaque frame pour le reste de la session.
            _done = true;

            if (SaveManager.GetIntValue(OpenedFlagKey, 0, false) != 0)
                return;

            SaveManager.SetIntValue(OpenedFlagKey, 1);

            Plugin.Log.LogInfo(
                $"[{nameof(ArchDoorUnlocker)}] Première session sur cette save : ouverture automatique des raccourcis du hub.");

            var loaded = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsSortMode.None);
            foreach (var key in HubShortcutKeys)
            {
                TrackedPeckState target = null;
                if (loaded != null)
                {
                    foreach (var state in loaded)
                    {
                        if (state != null && state.savableSystem == key)
                        {
                            target = state;
                            break;
                        }
                    }
                }

                if (target != null)
                {
                    target.SetState(1);
                    Plugin.Log.LogInfo($"[{nameof(ArchDoorUnlocker)}]   {key} : effet immédiat appliqué (objet trouvé en scène).");
                }
                else
                {
                    SaveManager.SetIntValue(key.ToString(), 1);
                    Plugin.Log.LogInfo($"[{nameof(ArchDoorUnlocker)}]   {key} : objet pas chargé, écriture SaveManager seule (effet au prochain chargement).");
                }
            }
        }
    }
}
