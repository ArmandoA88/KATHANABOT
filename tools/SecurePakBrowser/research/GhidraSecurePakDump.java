// Ghidra headless post-script for auditing KathanaGame's read-only SecurePak loader.
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
import ghidra.program.model.address.Address;
import ghidra.program.model.data.StringDataInstance;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;

public class GhidraSecurePakDump extends GhidraScript {
    private static final List<String> NEEDLES = Arrays.asList(
        "data.pak",
        "Unable to load data.pak",
        "PakCryptoException",
        "PakIntegrityException",
        "PakException",
        "SecurePak",
        "LZ4 decompression failed",
        "Zstd not available",
        "Unknown compression type"
    );

    private static final int MAX_CALL_DEPTH = 5;
    private static final int MAX_FUNCTIONS = 300;

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
        if (args.length != 1) {
            throw new IllegalArgumentException("Expected one output-file argument.");
        }

        File output = new File(args[0]);
        output.getParentFile().mkdirs();

        Listing listing = currentProgram.getListing();
        Set<Function> seedFunctions = new HashSet<>();
        List<String> evidence = new ArrayList<>();

        for (Data data : listing.getDefinedData(true)) {
            StringDataInstance stringData = StringDataInstance.getStringDataInstance(data);
            String value = stringData.getStringValue();
            if (value == null || !containsNeedle(value)) {
                continue;
            }

            Address address = data.getAddress();
            evidence.add(String.format("STRING %s: %s", address, value));
            for (Reference reference : getReferencesTo(address)) {
                Function owner = listing.getFunctionContaining(reference.getFromAddress());
                evidence.add(String.format("  REF %s from %s", reference.getFromAddress(),
                    owner == null ? "<no function>" : owner.getName()));
                if (owner != null) {
                    seedFunctions.add(owner);
                }
            }
        }

        SymbolIterator symbols = currentProgram.getSymbolTable().getAllSymbols(true);
        while (symbols.hasNext()) {
            Symbol symbol = symbols.next();
            if (containsNeedle(symbol.getName(true))) {
                evidence.add(String.format("SYMBOL %s: %s", symbol.getAddress(), symbol.getName(true)));
                Function owner = listing.getFunctionContaining(symbol.getAddress());
                if (owner != null) {
                    seedFunctions.add(owner);
                }
            }
        }

        // Include direct callers so constructors/wrappers around the archive loader are visible.
        List<Function> initialSeeds = new ArrayList<>(seedFunctions);
        for (Function seed : initialSeeds) {
            seedFunctions.addAll(seed.getCallingFunctions(monitor));
        }

        ArrayDeque<WorkItem> queue = new ArrayDeque<>();
        List<Function> ordered = new ArrayList<>();
        Set<Function> visited = new HashSet<>();
        for (Function seed : seedFunctions) {
            queue.add(new WorkItem(seed, 0));
        }

        while (!queue.isEmpty() && visited.size() < MAX_FUNCTIONS) {
            WorkItem item = queue.removeFirst();
            if (!visited.add(item.function)) {
                continue;
            }
            ordered.add(item.function);
            if (item.depth >= MAX_CALL_DEPTH) {
                continue;
            }
            for (Function called : item.function.getCalledFunctions(monitor)) {
                queue.addLast(new WorkItem(called, item.depth + 1));
            }
        }
        ordered.sort(Comparator.comparing(Function::getEntryPoint));

        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        try (PrintWriter writer = new PrintWriter(output, "UTF-8")) {
            writer.println("PROGRAM: " + currentProgram.getName());
            writer.println("IMAGE BASE: " + currentProgram.getImageBase());
            writer.println("FUNCTION COUNT: " + ordered.size());
            writer.println();
            writer.println("=== EVIDENCE ===");
            for (String line : evidence) {
                writer.println(line);
            }
            writer.println();

            for (Function function : ordered) {
                writer.printf("=== %s @ %s ===%n", function.getName(true), function.getEntryPoint());
                DecompileResults result = decompiler.decompileFunction(function, 60, monitor);
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

        println("SecurePak analysis written to " + output.getAbsolutePath());
    }

    private static boolean containsNeedle(String value) {
        String lower = value.toLowerCase();
        for (String needle : NEEDLES) {
            if (lower.contains(needle.toLowerCase())) {
                return true;
            }
        }
        return false;
    }
}
