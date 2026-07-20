// Dump selected bytes from an analyzed KathanaGame image.
// Usage: GhidraMemoryDump.java <output> <address> <length> [<address> <length> ...]
// @category Kathana

import java.io.File;
import java.io.PrintWriter;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;

public class GhidraMemoryDump extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 3 || (args.length % 2) == 0) {
            throw new IllegalArgumentException(
                "Expected <output> <address> <length> [<address> <length> ...].");
        }

        File output = new File(args[0]);
        output.getParentFile().mkdirs();
        try (PrintWriter writer = new PrintWriter(output, "UTF-8")) {
            for (int i = 1; i < args.length; i += 2) {
                Address start = toAddr(args[i]);
                int length = Integer.decode(args[i + 1]);
                byte[] bytes = new byte[length];
                currentProgram.getMemory().getBytes(start, bytes);
                writer.printf("%s (%d bytes):%n", start, length);
                for (int offset = 0; offset < bytes.length; offset += 16) {
                    writer.printf("  %s  ", start.add(offset));
                    int lineLength = Math.min(16, bytes.length - offset);
                    for (int column = 0; column < lineLength; column++) {
                        writer.printf("%02X ", bytes[offset + column] & 0xff);
                    }
                    writer.println();
                }
                writer.println("  references into range:");
                for (int offset = 0; offset < length; offset++) {
                    Address target = start.add(offset);
                    for (Reference reference : getReferencesTo(target)) {
                        Function owner = getFunctionContaining(reference.getFromAddress());
                        writer.printf("    %s -> %s (%s)%n", reference.getFromAddress(), target,
                            owner == null ? "no function" : owner.getName());
                    }
                }
            }
        }
        println("Memory dump written to " + output.getAbsolutePath());
    }
}
