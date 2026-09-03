using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

// NOTE IMPORTANT SUR LES RÉFÉRENCES :
// Ce projet doit référencer l'assembly interop générée automatiquement par
// BepInEx IL2CPP (via Il2CppInterop) au premier lancement du jeu avec BepInEx
// installé, PAS le DLL "dummy" généré par Il2CppInspectorRedux qu'on a utilisé
// pour lire le code. Cette assembly se trouve normalement dans :
//   <dossier du jeu>/BepInEx/interop/Assembly-CSharp.dll
// C'est celle-là qu'il faut ajouter comme référence de projet (elle contient
// les VRAIES méthodes invocables au runtime, générées par Il2CppInterop à
// partir des mêmes métadonnées IL2CPP qu'on a explorées avec Il2CppInspector).

namespace BigWalkArchipelago
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.yourname.bigwalk.archipelago.poc";
        public const string PluginName = "Big Walk Archipelago POC";
        public const string PluginVersion = "0.1.0";

        public override void Load()
        {
            Log.LogInfo($"{PluginName} v{PluginVersion} chargé.");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(GourdInterceptPatch));

            Log.LogInfo("Patch Harmony sur RewardGourd.ServerSetGourdState appliqué.");
        }
    }

    /// <summary>
    /// Intercepte chaque changement d'état d'un gourd. Dès qu'un gourd passe à
    /// "Loose" (= vient d'être débloqué par la résolution de son énigme, l'étau
    /// vient de le relâcher), on le fait immédiatement disparaître (Hidden) avant
    /// que le joueur ait eu la possibilité de le "peck" et de l'emmener localement.
    ///
    /// Objectif du POC : prouver qu'on peut intercepter le moment exact du
    /// déblocage, condition nécessaire pour plus tard remplacer cette action par
    /// l'envoi d'un check Archipelago au lieu de donner le gourd physiquement.
    /// </summary>
    [HarmonyPatch(typeof(RewardGourd), nameof(RewardGourd.ServerSetGourdState))]
    public static class GourdInterceptPatch
    {
        // Postfix : s'exécute APRÈS que le jeu a appliqué le nouvel état.
        // On lit newGourdState (le paramètre passé à l'appel qu'on intercepte),
        // pas __instance.gourdState, pour être sûr de réagir au bon événement
        // même si la propriété a déjà changé plusieurs fois entre-temps.
        static void Postfix(RewardGourd __instance, GourdFlag.GourdState newGourdState)
        {
            // On ne réagit qu'à la transition vers Loose (déblocage).
            // Sans cette garde, le ré-appel ci-dessous (avec Hidden) redéclencherait
            // ce postfix indéfiniment.
            if (newGourdState != GourdFlag.GourdState.Loose)
                return;

            var prop = __instance.prop;
            string propName = "<prop inconnu>";
            if (prop != null)
            {
                propName = prop.saveablePropName.ToString();
            }

            Plugin.Log.LogInfo(
                $"[AP-POC] Énigme résolue -> gourd '{propName}' débloqué (Loose). " +
                "Interception : on le fait disparaître avant que le joueur puisse le prendre."
            );

            // Renvoie immédiatement l'état à Hidden. Ce nouvel appel repasse par
            // ServerSetGourdState (donc par ce même postfix), mais la garde du
            // dessus (newGourdState != Loose) empêche toute boucle infinie.
            __instance.ServerSetGourdState(GourdFlag.GourdState.Hidden);
        }
    }
}
