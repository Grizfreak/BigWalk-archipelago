// Décompile les fonctions dont les adresses sont passées en arguments.
// Lancé par analyzeHeadless sur le projet Ghidra déjà analysé.
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.util.task.ConsoleTaskMonitor;

public class DecompileAt extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);

        for (String raw : args) {
            println("======================================================");
            Address addr = currentProgram.getAddressFactory().getAddress(raw);
            if (addr == null) {
                println("ADRESSE ILLISIBLE: " + raw);
                continue;
            }

            Function func = getFunctionContaining(addr);
            if (func == null) {
                println("PAS DE FONCTION a " + raw);
                continue;
            }

            println("FONCTION " + func.getName() + " @ " + func.getEntryPoint());
            println("======================================================");

            DecompileResults res = decomp.decompileFunction(func, 180, new ConsoleTaskMonitor());
            if (res.decompileCompleted()) {
                println(res.getDecompiledFunction().getC());
            } else {
                println("ECHEC DECOMPILATION: " + res.getErrorMessage());
            }
        }
    }
}
