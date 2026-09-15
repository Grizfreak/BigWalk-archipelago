namespace BigWalkArchipelago.Core
{
    // Valeur brute saisie dans le champ host:port ajouté sur l'écran
    // d'hébergement (Patches/HostMenuConfirmPatch.cs) — pas encore de parsing
    // host/port séparé ni de consommateur : le vrai client réseau AP (pas
    // encore écrit) lira cette valeur au moment de se connecter. Gardé
    // volontairement minimal tant qu'il n'y a rien de plus à en faire.
    internal static class ApSessionConfig
    {
        internal static string HostAndPort = string.Empty;
    }
}
