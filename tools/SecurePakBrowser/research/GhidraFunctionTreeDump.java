// Decompile a bounded internal call tree from one or more function addresses.
// Usage: GhidraFunctionTreeDump.java <output> <depth> <address> [address...]
// @category Kathana

import java.io.File;
import java.io.PrintWriter;
import java.util.ArrayDeque;
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

public class GhidraFunctionTreeDump extends GhidraScript {
    private static final int MAX_FUNCTIONS = 600;

    private static final class WorkItem {
        final Function function;
        final int depth;

        WorkItem(Function function, int depth) {
            this.function = function;
            this.depth = depth;
        }
    }

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 3) {
            throw new IllegalArgumentException("Expected <output> <depth> <address> [address...].");
        }

        File output = new File(args[0]);
        output.getParentFile().mkdirs();
        int maxDepth = Integer.parseInt(args[1]);

        ArrayDeque<WorkItem> queue = new ArrayDeque<>();
        for (int i = 2; i < args.length; i++) {
            Address address = toAddr(args[i]);
            Function function = getFunctionContaining(address);
            if (function == null) {
                throw new IllegalArgumentException("No function at " + args[i]);
            }
            queue.add(new WorkItem(function, 0));
        }

        List<Function> ordered = new ArrayList<>();
        Set<Function> visited = new HashSet<>();
        while (!queue.isEmpty() && visited.size() < MAX_FUNCTIONS) {
            WorkItem item = queue.removeFirst();
            if (item.function.isExternal() || !visited.add(item.function)) {
                continue;
            }
            ordered.add(item.function);
            if (item.depth >= maxDepth) {
                continue;
            }
            for (Function called : item.function.getCalledFunctions(monitor)) {
                if (!called.isExternal()) {
                    queue.addLast(new WorkItem(called, item.depth + 1));
                }
            }
        }
        ordered.sort(Comparator.comparing(Function::getEntryPoint));

        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        try (PrintWriter writer = new PrintWriter(output, "UTF-8")) {
            writer.println("PROGRAM: " + currentProgram.getName());
            writer.println("FUNCTION COUNT: " + ordered.size());
            writer.println();
            for (Function function : ordered) {
                writer.printf("=== %s @ %s ===%n", function.getName(true), function.getEntryPoint());
                DecompileResults result = decompiler.decompileFunction(function, 90, monitor);
                if (result.decompileCompleted()) {
                    writer.println(result.getDecompiledFunction().getC());
                } else {
                    writer.println("<decompile failed: " + result.getErrorMessage() + ">");
                }
                writer.println();
            }
        } finally {
            decompiler.dispose();
        }

        println("Function tree written to " + output.getAbsolutePath());
    }
}
