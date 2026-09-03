namespace BigWalkArchipelago.Core
{
    // Un patch appelle Plugin.Reporter.ReportCheck(id) sans savoir ce qui se
    // passe derrière : logguer localement aujourd'hui, parler au serveur
    // Archipelago demain — aucune ligne à toucher dans Patches/ pour ce switch.
    internal interface ICheckReporter
    {
        void ReportCheck(string locationId);
    }
}
