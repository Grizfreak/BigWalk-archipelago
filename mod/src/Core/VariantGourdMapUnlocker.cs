using System;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Révèle automatiquement sur la carte les gourds "variant challenge"
    // (violettes/postgame), en continu — pas une seule fois par save comme
    // Core/ArchDoorUnlocker.cs : GourdMap.Initialize() remet systématiquement
    // l'icône à Hidden à CHAQUE nouvelle instance de GourdMap (donc à chaque
    // changement de zone), cf. VariantGourdRevealer. Poll à intervalle plutôt
    // qu'à chaque frame (FindObjectsByType a un coût) ; GourdFlag.SetState a
    // un early-return no-op si l'état ne change pas, donc rappeler sur un
    // gourd déjà révélé ne fait rien. Purement un état d'affichage
    // local/carte (pas de SaveManager, pas de réseau) : pas besoin de garde
    // NetworkServer.active, s'exécute sur chaque client indépendamment.
    internal class VariantGourdMapUnlocker : MonoBehaviour
    {
        // Constructeur requis par Il2CppInterop pour tout type injecté en IL2CPP.
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
            VariantGourdRevealer.RevealAll();
        }
    }
}
