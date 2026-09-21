// Cherche les fonctions dont le nom contient l'argument, et liste qui les
// appelle. Sert à remonter d'un champ réseau vers le code qui l'écrit,
// plutôt que de deviner quelle méthode « a l'air » responsable.
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.util.task.ConsoleTaskMonitor;

public class FindCallers extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length == 0) {
            println("Usage: FindCallers <fragment de nom>");
            return;
        }

        String needle = args[0].toLowerCase();
        ConsoleTaskMonitor monitor = new ConsoleTaskMonitor();
        FunctionIterator it = currentProgram.getFunctionManager().getFunctions(true);
        int found = 0;

        while (it.hasNext() && !monitor.isCancelled()) {
            Function f = it.next();
            if (!f.getName().toLowerCase().contains(needle)) {
                continue;
            }

            found++;
            println("=== FONCTION " + f.getName() + " @ " + f.getEntryPoint());
            for (Function caller : f.getCallingFunctions(monitor)) {
                println("      <- " + caller.getName() + " @ " + caller.getEntryPoint());
            }
        }

        println("=== TOTAL: " + found + " fonction(s) correspondant a '" + args[0] + "'");
    }
}
