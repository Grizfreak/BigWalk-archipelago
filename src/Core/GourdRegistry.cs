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

        // Contrairement à gourdXxx/valetXxx, les big keys sont nommées par
        // zone d'origine (couleur) alors que leurs plinthes sont nommées par
        // lieu physique de dépôt : aucune correspondance dérivable par nom.
        // Confirmé par le joueur en jeu (session du 2026-09-07, cf.
        // big-walk-archipelago-notes.md) — pas décompilable via Ghidra, ce
        // câblage n'existe que dans les assets de scène Unity.
        private static readonly Dictionary<SaveablePropName, SaveableHomeName> BigKeyHomesByProp = new()
        {
            { SaveablePropName.bigKeyIntro, SaveableHomeName.bigKeyPlinthIntro },
            { SaveablePropName.bigKeyRedZone, SaveableHomeName.bigKeyPlinthMapRoom },
            { SaveablePropName.bigKeyGreenZone, SaveableHomeName.bigKeyPlinthSkiLift },
            { SaveablePropName.bigKeyBlueZone, SaveableHomeName.bigKeyPlinthTrain },
            { SaveablePropName.bigKeyYellowZone, SaveableHomeName.bigKeyPlinthTunnels },
            { SaveablePropName.bigKeyBoss, SaveableHomeName.bigKeyPlinthEnding },
            { SaveablePropName.bigKeyOverflow, SaveableHomeName.bigKeyPlinthGoodbye2 },
        };

        // 3 gourds dont le valet correspondant n'est PAS la substitution
        // naïve "gourd"->"valet" : faute de frappe côté jeu lui-même entre les
        // deux enums (trouvé en comparant les 58 paires une par une, cf.
        // big-walk-archipelago-notes.md, session du 2026-09-07). Sans cette
        // table, TryGetHomeName échoue silencieusement pour ces 3 gourds
        // (Enum.TryParse ne trouve pas le nom généré) et ItemApplier ignore
        // l'item correspondant.
        private static readonly Dictionary<SaveablePropName, SaveableHomeName> GourdHomeNameExceptions = new()
        {
            { SaveablePropName.gourdCenturonSong, SaveableHomeName.valetCenturionSong },
            { SaveablePropName.gourdSingerAndSelecter, SaveableHomeName.valetSingerAndSelector },
            { SaveablePropName.gourdDancerAndSelecter, SaveableHomeName.valetDancerAndSelector },
        };

        internal static bool TryGetLocationId(SaveablePropName propName, out string locationId)
        {
            return LocationIdsByProp.TryGetValue(propName, out locationId);
        }

        // Distingue les deux chemins de réception d'item dans ItemApplier
        // (ApplyBigKeyItem, 1:1, vs ApplyGourdItem, générique) : la table des
        // 7 big keys ci-dessus est déjà la source de vérité pour "quels
        // SaveablePropName sont des big keys", pas la peine de la dupliquer.
        internal static bool IsBigKey(SaveablePropName propName)
        {
            return BigKeyHomesByProp.ContainsKey(propName);
        }

        // Convention de nommage confirmée en jeu (cf. big-walk-archipelago-notes.md) :
        // gourdXxx (SaveablePropName, le prop-récompense) correspond 1:1 à
        // valetXxx (SaveableHomeName, son emplacement de rangement/couffin).
        // Centralisé ici (plutôt que dupliqué dans chaque appelant) car utilisé
        // à la fois par le debug (DebugGourdUnlocker) et par la matérialisation
        // d'un item reçu (ItemApplier).
        internal static bool TryGetHomeName(SaveablePropName propName, out SaveableHomeName homeName)
        {
            if (BigKeyHomesByProp.TryGetValue(propName, out homeName))
                return true;

            if (GourdHomeNameExceptions.TryGetValue(propName, out homeName))
                return true;

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
