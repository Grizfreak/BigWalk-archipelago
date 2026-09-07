namespace BigWalkArchipelago.Core
{
    // Dédoublonnage partagé entre tous les chemins de code qui peuvent
    // détecter le même check. Confirmé nécessaire en session de test le
    // 2026-09-03 : RewardGourd.ServerSetGourdState(Loose) ET
    // PeckEffectSavableHome.Peck() (sur le "vice launch switch") écrivent
    // tous les deux dans SaveManager.SetIntValue pour le même gourd.
    //
    // Persisté dans SaveManager (pas un simple HashSet en mémoire) : confirmé
    // en test le 2026-09-07, un HashSet en mémoire ne protège que DANS la
    // session en cours. Au chargement d'une zone, le jeu réaffirme l'état
    // d'un gourd déjà résolu par (au moins) deux chemins indépendants —
    // Prop.Start() qui repin le prop, ET un rappel de
    // RewardGourd.ServerSetGourdState(Loose) — chacun redéclenchant
    // GourdStatePatch/SaveValuePatch à chaque redémarrage du jeu si le
    // dédoublonnage ne survit pas à la session. Le préfixe "ap_reported_"
    // évite toute collision avec les clés SaveablePropName/SaveableHomeName
    // normales du jeu.
    internal static class CheckTracker
    {
        private const string KeyPrefix = "ap_reported_";

        internal static bool TryMarkReported(string locationId)
        {
            var key = KeyPrefix + locationId;
            if (SaveManager.GetIntValue(key, 0, false) != 0)
                return false;

            SaveManager.SetIntValue(key, 1);
            return true;
        }
    }
}
