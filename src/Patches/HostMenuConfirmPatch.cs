using System;
using BigWalkArchipelago.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace BigWalkArchipelago.Patches
{
    // Ajoute un champ host:port AP sur l'écran d'hébergement (HostMenuConfirm)
    // — préalable au vrai client réseau AP (pas encore écrit), qui lira
    // Core.ApSessionConfig.HostAndPort au moment de se connecter. Layout
    // confirmé via Debug/DebugMenuLookup.cs (touche M) le 2026-09-15 :
    // GameSlotCard_Editable positionne tout à la main (pas de LayoutGroup),
    // avec GameName en pleine largeur (900) sur sa propre ligne, et
    // Password/LastPlayed côte à côte en dessous (399.42 de large chacun,
    // décalage de 489.4 entre les deux). On reproduit exactement ce schéma
    // pour la ligne GameName : rétrécie à 399.42, avec le nouveau champ
    // cloné à sa droite (décalage 489.4, même Y) — décision du joueur
    // (2026-09-15) de placer le champ "en face du nom de la sauvegarde".
    //
    // Clonage plutôt que construction from scratch (même philosophie que
    // ReceivedItemSpawner pour les gourds cosmétiques) : GameNameInput porte
    // MultiPlatformInputField (sous-classe de TMP_InputField, confirmé dans
    // il2cpp.cs) avec toute la navigation clavier/manette/style déjà câblée
    // — bien plus sûr que d'assembler un TMP_InputField à la main.
    [HarmonyPatch(typeof(HostMenuConfirm), nameof(HostMenuConfirm.OnEnable))]
    internal static class HostMenuConfirmPatch
    {
        private const string HostPortRowName = "ApHostPortRow";
        private const float HalfRowWidth = 399.42f;
        private const float ColumnOffsetX = 489.4f; // Password -> LastPlayed, mesuré via DebugMenuLookup

        private static void Postfix(HostMenuConfirm __instance)
        {
            // Décompilation Ghidra du 2026-09-15 (HouseAuthenticator.
            // OnPasswordResponseMessage, HostMenuConfirm.RequiredInputsHaveValues/
            // IsReadyToContinue/ActionStart) : passwordRequired est un simple
            // booléen sérialisé qui gate la validation du bouton "Continuer",
            // rien côté Mirror/HouseAuthenticator n'exige un mot de passe
            // non-vide (comparaison de chaînes ordinaire, vide == vide passe).
            // Le mettre à false rend juste le champ optionnel : s'il reste
            // rempli, sa valeur est quand même écrite/transmise normalement
            // (ActionStart ne conditionne l'écriture à rien d'autre).
            __instance.passwordRequired = false;

            try
            {
                InjectHostPortField(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(HostMenuConfirmPatch)}] Échec de l'injection du champ host:port, ignoré : {ex.Message}");
            }
        }

        private static void InjectHostPortField(HostMenuConfirm menu)
        {
            var gameNameInput = menu.gameNameField;
            var gameNameRow = gameNameInput != null ? gameNameInput.transform.parent : null;
            var parent = gameNameRow != null ? gameNameRow.parent : null;
            if (gameNameRow == null || parent == null || gameNameRow.name != "GameName")
                return;

            // OnEnable peut être rappelé plusieurs fois sur la même instance
            // (le joueur quitte/rouvre l'écran) : ne pas re-cloner à chaque fois.
            if (parent.Find(HostPortRowName) != null)
                return;

            var gameNameRect = gameNameRow.GetComponent<RectTransform>();
            var originalWidth = gameNameRect.sizeDelta.x;
            var scale = HalfRowWidth / originalWidth;

            ScaleRowWidth(gameNameRow, scale, HalfRowWidth);

            var clone = UnityEngine.Object.Instantiate(gameNameRow.gameObject, parent);
            clone.name = HostPortRowName;

            var cloneRect = clone.transform.GetComponent<RectTransform>();
            cloneRect.anchoredPosition = new Vector2(
                gameNameRect.anchoredPosition.x + ColumnOffsetX,
                gameNameRect.anchoredPosition.y);

            RetitleClone(clone.transform);
            RelabelOriginalFields(menu, gameNameRow);

            Plugin.Log.LogInfo($"[{nameof(HostMenuConfirmPatch)}] Champ host:port injecté sur '{menu.gameObject.name}'.");
        }

        // Les 2 champs existants sont réutilisés comme identifiants AP (slot
        // name / mot de passe AP), pas comme nom/mot de passe de la session
        // locale — décision du joueur (2026-09-15). Ne renomme QUE les
        // libellés affichés : la logique (ActionStart, SaveData.slotName/
        // password, NetworkMinder.SetServerPassword) reste inchangée, c'est
        // le vrai client réseau AP (pas encore écrit) qui lira ces mêmes
        // champs différemment le moment venu.
        private static void RelabelOriginalFields(HostMenuConfirm menu, Transform gameNameRow)
        {
            SetStaticLabel(gameNameRow.Find("GameNameTitle"), "SLOT NAME :");

            var passwordRow = menu.passwordField != null ? menu.passwordField.transform.parent : null;
            if (passwordRow != null && passwordRow.name == "Password")
                SetStaticLabel(passwordRow.Find("PasswordTitle"), "ARCHIPELAGO PASSWORD :");
        }

        // Rétrécit la ligne GameName ET ses deux enfants directs
        // (GameNameTitle/GameNameInput) proportionnellement — pas de
        // LayoutGroup sur ce panneau, tout est en sizeDelta fixe, donc
        // resize le parent seul ne toucherait pas les enfants.
        private static void ScaleRowWidth(Transform row, float scale, float newRowWidth)
        {
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(newRowWidth, rowRect.sizeDelta.y);

            for (var i = 0; i < row.childCount; i++)
            {
                var childRect = row.GetChild(i).GetComponent<RectTransform>();
                if (childRect == null)
                    continue;

                childRect.sizeDelta = new Vector2(childRect.sizeDelta.x * scale, childRect.sizeDelta.y);
            }
        }

        private static void RetitleClone(Transform clone)
        {
            var title = clone.Find("GameNameTitle");
            SetStaticLabel(title, "HÔTE ARCHIPELAGO :");

            var input = clone.Find("GameNameInput");
            if (input == null)
                return;

            var placeholder = input.Find("Text Area/Placeholder");
            SetStaticLabel(placeholder, "hôte:port");

            var tmpInput = input.GetComponent<TMP_InputField>();
            if (tmpInput == null)
                return;

            // Cloné de GameNameInput, qui limite la longueur d'un nom de
            // sauvegarde (characterLimit hérité, trop court pour une adresse
            // "archipelago.gg:38281" ou un hostname perso) — retiré, aucune
            // raison de limiter la longueur d'un host:port (signalé par le
            // joueur, 2026-09-15 : impossible de taper assez de caractères).
            tmpInput.characterLimit = 0;

            // Pré-rempli avec la dernière valeur saisie (persistée dans le
            // .cfg BepInEx, ModConfig.ArchipelagoHostPort — "archipelago.gg:"
            // par défaut au tout premier lancement) plutôt qu'une valeur figée
            // à chaque fois — demande du joueur (2026-09-15) de ne pas avoir à
            // la retaper à chaque partie.
            var lastValue = ModConfig.ArchipelagoHostPort.Value;
            tmpInput.text = lastValue;
            ApSessionConfig.HostAndPort = lastValue;
            tmpInput.onValueChanged.AddListener((UnityAction<string>)(value =>
            {
                ApSessionConfig.HostAndPort = value;
                ModConfig.ArchipelagoHostPort.Value = value;
            }));
        }

        // Les libellés (GameNameTitle, Placeholder) portent un LocalizedText
        // qui réécrit le texte depuis une clé de traduction — sans le
        // désactiver, notre texte serait écrasé au prochain refresh de
        // langue (voire immédiatement, LocalizedText pouvant réappliquer sa
        // traduction dans son propre OnEnable, rejoué par Instantiate).
        private static void SetStaticLabel(Transform target, string text)
        {
            if (target == null)
                return;

            var localized = target.GetComponent<LocalizedText>();
            if (localized != null)
                localized.enabled = false;

            var label = target.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = text;
        }
    }
}
