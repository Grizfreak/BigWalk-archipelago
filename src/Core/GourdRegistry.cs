using System;
using System.Collections.Generic;

namespace BigWalkArchipelago.Core
{
    // Traduit un SaveablePropName (identifiant interne du jeu) en id de
    // location Archipelago. Pour l'instant l'id de location est simplement le
    // nom de l'enum lui-même (ex. "gourdCabinFever") : c'est une clé stable et
    // lisible, suffisante tant qu'on ne branche pas le vrai protocole
    // Archipelago. Seul endroit à modifier pour changer la correspondance ou
    // exclure d'autres valeurs.
    internal static class GourdRegistry
    {
        private static readonly Dictionary<SaveablePropName, string> LocationIdsByProp = BuildLocationIds();

        internal static bool TryGetLocationId(SaveablePropName propName, out string locationId)
        {
            return LocationIdsByProp.TryGetValue(propName, out locationId);
        }

        // Convention de nommage confirmée en jeu (cf. big-walk-archipelago-notes.md) :
        // gourdXxx (SaveablePropName, le prop-récompense) correspond 1:1 à
        // valetXxx (SaveableHomeName, son emplacement de rangement/couffin).
        // Centralisé ici (plutôt que dupliqué dans chaque appelant) car utilisé
        // à la fois par le debug (DebugGourdUnlocker) et par la matérialisation
        // d'un item reçu (ItemApplier).
        internal static bool TryGetHomeName(SaveablePropName propName, out SaveableHomeName homeName)
        {
            var name = propName.ToString();
            if (name.StartsWith("gourd", StringComparison.Ordinal))
                return Enum.TryParse("valet" + name.Substring("gourd".Length), out homeName);

            homeName = default;
            return false;
        }

        private static Dictionary<SaveablePropName, string> BuildLocationIds()
        {
            var map = new Dictionary<SaveablePropName, string>();

            foreach (SaveablePropName propName in Enum.GetValues(typeof(SaveablePropName)))
            {
                if (propName == SaveablePropName.notSavable)
                    continue;

                // Exclut gourdTesting00-39 ainsi que bigKeyTesting0-4 /
                // bigKeyTestingOverflow (même famille de valeurs de dev que le
                // jeu ne déclenche jamais en jeu normal) : aucune de ces
                // valeurs n'est un vrai check.
                if (propName.ToString().Contains("Testing", StringComparison.OrdinalIgnoreCase))
                    continue;

                map[propName] = propName.ToString();
            }

            return map;
        }
    }
}
