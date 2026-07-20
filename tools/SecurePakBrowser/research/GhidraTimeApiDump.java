// Locate timing imports and decompile their direct and second-level callers.
// @category Kathana

import java.io.File;
import java.io.PrintWriter;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;

public class GhidraTimeApiDump extends GhidraScript {
    private static final List<String> NEEDLES = Arrays.asList(
        "GetTickCount",
        "GetTickCount64",
        "QueryPerformanceCounter",
        "timeGetTime",
        "GetSystemTimeAsFileTime",
        "NtQueryPerformanceCounter"
    );

    private static final class WorkItem {
        final Function function;
        final int depth;
        WorkItem(Function function, int depth) { this.function = function; this.depth = depth; }
    }

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 1) throw new IllegalArgumentException("Expected one output-file argument.");
        File output = new File(args[0]);
        output.getParentFile().mkdirs();

        Listing listing = currentProgram.getListing();
        Set<Function> directCallers = new HashSet<>();
        List<String> evidence = new ArrayList<>();
        SymbolIterator symbols = currentProgram.getSymbolTable().getAllSymbols(true);
        while (symbols.hasNext()) {
            Symbol symbol = symbols.next();
            if (!matches(symbol.getName(true))) continue;
            evidence.add(String.format("SYMBOL %s @ %s", symbol.getName(true), symbol.getAddress()));
            for (Reference reference : getReferencesTo(symbol.getAddress())) {
                Function caller = listing.getFunctionContaining(reference.getFromAddress());
                evidence.add(String.format("  REF %s owner=%s", reference.getFromAddress(),
                    caller == null ? "<none>" : caller.getName(true)));
                if (caller != null) directCallers.add(caller);
            }
        }

        ArrayDeque<WorkItem> queue = new ArrayDeque<>();
        Set<Function> visited = new HashSet<>();
        List<Function> targets = new ArrayList<>();
        for (Function caller : directCallers) queue.add(new WorkItem(caller, 0));
        while (!queue.isEmpty() && visited.size() < 500) {
            WorkItem item = queue.removeFirst();
            if (!visited.add(item.function)) continue;
            targets.add(item.function);
            if (item.depth >= 2) continue;
            for (Function caller : item.function.getCallingFunctions(monitor)) {
                queue.addLast(new WorkItem(caller, item.depth + 1));
            }
        }
        targets.sort(Comparator.comparing(Function::getEntryPoint));

        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        try (PrintWriter writer = new PrintWriter(output, "UTF-8")) {
            writer.println("PROGRAM: " + currentProgram.getName());
            writer.println("TIMING SYMBOL EVIDENCE: " + evidence.size());
            for (String line : evidence) writer.println(line);
            writer.println("CALLER FUNCTIONS: " + targets.size());
            writer.println();
            for (Function function : targets) {
                writer.printf("=== %s @ %s ===%n", function.getName(true), function.getEntryPoint());
                DecompileResults result = decompiler.decompileFunction(function, 60, monitor);
                writer.println(result.decompileCompleted()
                    ? result.getDecompiledFunction().getC()
                    : "<decompile failed: " + result.getErrorMessage() + ">");
                writer.println();
            }
        } finally {
            decompiler.dispose();
        }
        println("Time API analysis written to " + output.getAbsolutePath());
    }

    private static boolean matches(String value) {
        String lower = value.toLowerCase();
        for (String needle : NEEDLES) {
            if (lower.contains(needle.toLowerCase())) return true;
        }
        return false;
    }
}
