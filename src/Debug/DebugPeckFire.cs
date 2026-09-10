using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Outil d'action (pas un simple diagnostic) : permet de déclencher un
    // PeckSwitch à distance, sans avoir à s'y tenir physiquement devant.
    // Utile pour tester seul un mécanisme conçu pour 2 joueurs (ex. les
    // boutons "N-hold" simultanés des cloches/du Gauntlet, cf.
    // big-walk-archipelago-notes.md) : un joueur peck normalement le premier
    // bouton, puis déclenche ce hotkey pour peck le second à distance par
    // son nom (repéré au préalable via F9/Delete), sans avoir besoin d'un
    // second joueur physiquement présent.
    //
    // PeckSwitch.Peck() est la même méthode publique appelée par le jeu lui-
    // même lors d'une interaction normale (confirmé via il2cpp.cs) — ce n'est
    // pas un contournement, juste un appel à distance de la même action.
    internal static class DebugPeckFire
    {
        // Rayon de sécurité : beaucoup de noms de PeckSwitch sont génériques
        // et réutilisés partout dans le jeu (ex. "UpSwitch", vu identique à
        // la cloche de la chapelle, à l'entrée du Gauntlet ET à la cloche du
        // sommet) — sans limite de distance, FireByName déclencherait TOUS
        // les objets portant ce nom dans toute la zone chargée, pas
        // seulement celui visé à côté du joueur.
        private const float MaxFireRadius = 40f;

        internal static void FireByName(string nameSubstring)
        {
            if (string.IsNullOrWhiteSpace(nameSubstring))
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Aucun nom configuré (ModConfig.RemotePeckSwitchName vide) — rien à déclencher.");
                return;
            }

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = MaxFireRadius * MaxFireRadius;

            var all = UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Aucun PeckSwitch trouvé dans la zone actuelle.");
                return;
            }

            var fired = 0;
            foreach (var sw in all)
            {
                if (sw == null)
                    continue;

                string goName;
                float sqrDistance;
                try
                {
                    goName = sw.gameObject.name;
                    sqrDistance = (sw.transform.position - origin).sqrMagnitude;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckFire)}] PeckSwitch illisible (nom/position) : {ex.Message}");
                    continue;
                }

                if (goName.IndexOf(nameSubstring, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Filtré par distance en dernier (pas dans la boucle de nom)
                // pour que le message "aucun match" distingue "nom inconnu"
                // de "nom connu mais trop loin" — voir le log ci-dessous.
                if (sqrDistance > sqrRadius)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPeckFire)}]   {goName} correspond au nom mais est à {Math.Sqrt(sqrDistance):F1}m (> {MaxFireRadius}m) — ignoré par sécurité.");
                    continue;
                }

                try
                {
                    sw.Peck();
                    fired++;
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Peck() déclenché à distance sur {goName}.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckFire)}] {goName} — exception pendant Peck(), ignorée : {ex.Message}");
                }
            }

            if (fired == 0)
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Aucun PeckSwitch dont le nom contient '{nameSubstring}' trouvé à moins de {MaxFireRadius}m.");
        }
    }
}
