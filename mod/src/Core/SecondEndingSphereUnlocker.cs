using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Désactive la sphère noire bloquante du hub (entrée de la "deuxième fin"
    // / postgame) dès la première session sur une save donnée. Décision
    // utilisateur (cf. big-walk-archipelago-notes.md, session du 2026-09-11) :
    // en vanilla, cette sphère ne se casse qu'après une première complétion
    // du jeu — condition non identifiable avec certitude après plusieurs
    // sessions d'investigation (Ghidra + dump en jeu), et l'hypothèse la plus
    // prometteuse ("le remplissage de la plinthe bigKeyPlinthGoodbye2, juste
    // derrière, déclenche la casse") s'est révélée être l'inverse : cette
    // plinthe n'est accessible QUE si la sphère est déjà cassée. Plutôt que
    // de continuer à chercher le vrai flag/mécanisme Peck, la sphère est
    // simplement désactivée à chaque session : pas d'intérêt à rester
    // fermée pour un monde Archipelago, même philosophie que ArchDoorUnlocker
    // pour les raccourcis du hub.
    //
    // Différence clé avec ArchDoorUnlocker : là-bas, l'effet (une écriture
    // TrackedPeckState/SaveManager) est persisté par le jeu lui-même, donc un
    // flag "déjà fait une fois" suffit à ne plus jamais y retoucher. Ici,
    // SetActive(false) est un état purement local à la session (rien n'est
    // sauvegardé) : il faut donc le rejouer à CHAQUE démarrage, sans jamais
    // se fier à un flag SaveManager pour sauter l'étape.
    //
    // Ciblage par préfixe de nom, restreint à la sphère elle-même — PAS
    // "Spawn_SecondEnding" tout court. Ce préfixe plus large avait été
    // essayé en premier et attrapait aussi `Spawn_SecondEnding_Door (1)` :
    // confirmé en jeu (2026-09-11) que ça désactivait la vraie porte
    // d'entrée de la deuxième fin en plus de la sphère, ouvrant l'accès à
    // cette zone dès le début de partie — pas voulu, seule la sphère doit
    // disparaître, la progression réelle vers la deuxième fin (Gauntlet,
    // cloches, etc.) doit rester intacte. `Spawn_SecondEnding_Sphere` cible
    // donc précisément `Spawn_SecondEnding_Sphere_Whole` (mesh+collider de
    // la sphère) sans toucher à `Spawn_SecondEnding_Door (1)` ni à quoi que
    // ce soit d'autre du même ensemble scène.
    internal class SecondEndingSphereUnlocker : MonoBehaviour
    {
        // Constructeur requis par Il2CppInterop pour tout type injecté en IL2CPP.
        public SecondEndingSphereUnlocker(IntPtr ptr) : base(ptr)
        {
        }

        private const string BlockingObjectNamePrefix = "Spawn_SecondEnding_Sphere";

        private bool _done;

        private void Update()
        {
            if (_done)
                return;

            // Modèle d'autorité host, comme le reste du mod (cf. ItemApplier).
            if (!NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            // Fixé dès la première frame où les deux conditions sont
            // réunies : évite de re-scanner tous les Transform chargés à
            // chaque frame pour le reste de la session, que la zone du hub
            // soit chargée ou non (si elle ne l'est pas, rien à faire ici de
            // toute façon).
            _done = true;

            var disabled = new List<string>();
            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null)
                    continue;

                var go = t.gameObject;
                if (!go.activeSelf)
                    continue;

                if (!go.name.StartsWith(BlockingObjectNamePrefix, StringComparison.Ordinal))
                    continue;

                go.SetActive(false);
                disabled.Add(go.name);
            }

            if (disabled.Count == 0)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(SecondEndingSphereUnlocker)}] Aucun objet '{BlockingObjectNamePrefix}*' trouvé dans la zone actuelle (hub pas chargé ?).");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(SecondEndingSphereUnlocker)}] {disabled.Count} objet(s) désactivé(s) : {string.Join(", ", disabled)}.");
        }
    }
}
