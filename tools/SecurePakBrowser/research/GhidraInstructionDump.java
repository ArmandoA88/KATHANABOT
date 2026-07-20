// Dump disassembly for selected functions.
// Usage: GhidraInstructionDump.java <output> <address> [address...]
// @category Kathana

import java.io.File;
import java.io.PrintWriter;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;

public class GhidraInstructionDump extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 2) {
            throw new IllegalArgumentException("Expected <output> <address> [address...].");
        }
        File output = new File(args[0]);
        output.getParentFile().mkdirs();
        try (PrintWriter writer = new PrintWriter(output, "UTF-8")) {
            for (int i = 1; i < args.length; i++) {
                Address address = toAddr(args[i]);
                Function function = getFunctionContaining(address);
                if (function == null) {
                    throw new IllegalArgumentException("No function at " + address);
                }
                writer.printf("=== %s @ %s ===%n", function.getName(), function.getEntryPoint());
                for (Instruction instruction : currentProgram.getListing().getInstructions(function.getBody(), true)) {
                    writer.printf("%s  %-8s %s%n", instruction.getAddress(),
                        instruction.getMnemonicString(), instruction.toString().substring(instruction.getMnemonicString().length()).trim());
                }
                writer.println();
            }
        }
        println("Instruction dump written to " + output.getAbsolutePath());
    }
}
