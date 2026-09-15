namespace BigWalkArchipelago.Core
{
    // Logique partagée entre le hotkey de debug (Debug/DebugVariantGourdReveal)
    // et l'automatisation (Core/VariantGourdMapUnlocker) : révèle sur la carte
    // les gourds "variant challenge" (violettes, normalement débloquées après
    // une première fin de partie). Cf. big-walk-archipelago-notes.md, session
    // du 2026-09-09 : GourdMap.Initialize() force GourdFlag.SetState(Hidden)
    // pour tout gourd isVariantChallenge==true à CHAQUE (re)chargement de
    // zone — un état purement local/session, jamais lu ni écrit dans
    // SaveManager.
    //
    // CORRIGÉ (2026-09-09) : la première version invoquait directement l'event
    // statique GourdMap.refreshFlag (Action<SaveablePropName, GourdState>).
    // Isolé en jeu par test A/B : révéler un gourd violet via cet Invoke()
    // avant de le résoudre provoquait, à chaque fois, un freeze total du jeu
    // (aucune exception, aucun log) quelques dizaines de secondes plus tard au
    // premier alt-tab — jamais reproduit sans cet appel (gourd normale, ou
    // gourd violette découverte/résolue sans jamais appeler refreshFlag).
    // Invoquer directement un delegate Il2Cpp statique depuis du code managé
    // externe semble corrompre un état interne (probablement côté interop
    // IL2CPP) qui ne se manifeste que plus tard. Fix : appeler
    // GourdFlag.SetState(...) directement sur l'instance trouvée (un appel de
    // méthode normal sur un composant, pas une invocation de delegate) — exactement
    // ce que fait la méthode privée GourdMap.RefreshFlag en interne, donc tout
    // aussi sûr côté jeu, sans passer par le point qui posait problème.
    internal static class VariantGourdRevealer
    {
        internal static int RevealAll()
        {
            var flags = UnityEngine.Object.FindObjectsByType<GourdFlag>(UnityEngine.FindObjectsSortMode.None);
            if (flags == null || flags.Length == 0)
                return 0;

            var variantProps = UnityEngine.Object.FindObjectsByType<RewardGourd>(UnityEngine.FindObjectsSortMode.None);
            if (variantProps == null || variantProps.Length == 0)
                return 0;

            var count = 0;
            foreach (var flag in flags)
            {
                if (flag == null || flag.gourdState != GourdFlag.GourdState.Hidden)
                    continue;

                var isVariant = false;
                foreach (var gourd in variantProps)
                {
                    if (gourd == null || gourd.prop == null)
                        continue;

                    if (gourd.prop.saveablePropName == flag.saveablePropName && gourd.isVariantChallenge)
                    {
                        isVariant = true;
                        break;
                    }
                }

                if (!isVariant)
                    continue;

                flag.SetState(GourdFlag.GourdState.Locked);
                count++;
            }

            return count;
        }
    }
}
