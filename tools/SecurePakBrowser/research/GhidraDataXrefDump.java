// Decompile every function that directly references one or more data addresses.
// Usage: GhidraDataXrefDump.java <output> <address> [address...]
// @category Kathana

import java.io.File;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;

public class GhidraDataXrefDump extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 2) {
            throw new IllegalArgumentException("Expected <output> <address> [address...].");
        }

        File output = new File(args[0]);
        output.getParentFile().mkdirs();
        Set<Function> functions = new HashSet<>();
        List<String> references = new ArrayList<>();

        for (int index = 1; index < args.length; index++) {
            Address target = toAddr(args[index]);
            for (Reference reference : getReferencesTo(target)) {
                Function function = getFunctionContaining(reference.getFromAddress());
                references.add(String.format("%s <- %s (%s)", target, reference.getFromAddress(),
                    function == null ? "<none>" : function.getName(true)));
                if (function != null && !function.isExternal()) {
                    functions.add(function);
                }
            }
        }

        List<Function> ordered = new ArrayList<>(functions);
        ordered.sort(Comparator.comparing(Function::getEntryPoint));
        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        try (PrintWriter writer = new PrintWriter(output, "UTF-8")) {
            for (String line : references) writer.println(line);
            writer.println("FUNCTION COUNT: " + ordered.size());
            writer.println();
            for (Function function : ordered) {
                writer.printf("=== %s @ %s ===%n", function.getName(true), function.getEntryPoint());
                DecompileResults result = decompiler.decompileFunction(function, 90, monitor);
                writer.println(result.decompileCompleted()
                    ? result.getDecompiledFunction().getC()
                    : "<decompile failed: " + result.getErrorMessage() + ">");
                writer.println();
            }
        } finally {
            decompiler.dispose();
        }
    }
}
