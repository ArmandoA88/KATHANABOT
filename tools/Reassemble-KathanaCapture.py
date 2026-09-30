"""Offline TCP reassembly and conservative structural checks; never sends traffic."""
import collections
import contextlib
import io
import json
import math
import pathlib
import runpy
import sys
import zlib


def reassemble(segments):
    anchor = segments[0]['seq']
    cells = {}
    overlaps = conflicts = 0
    for segment in segments:
        offset = ((segment['seq'] - anchor + 2**31) % 2**32) - 2**31
        for index, value in enumerate(segment['payload'], offset):
            if index in cells:
                overlaps += 1
                conflicts += cells[index] != value
            else:
                cells[index] = value
    runs = []
    for index in sorted(cells):
        if not runs or index != runs[-1][0] + len(runs[-1][1]):
            runs.append((index, bytearray()))
        runs[-1][1].append(cells[index])
    gaps = [b[0] - (a[0] + len(a[1])) for a, b in zip(runs, runs[1:])]
    return anchor, runs, gaps, overlaps, conflicts


def entropy(blob):
    return -sum((n / len(blob)) * math.log2(n / len(blob)) for n in collections.Counter(blob).values()) if blob else 0


def inspect(blob):
    # Signature hits are only candidates; bounded decompression must complete.
    compressed = []
    for offset in range(len(blob) - 2):
        gzip = blob[offset:offset+3] == b'\x1f\x8b\x08'
        zheader = blob[offset:offset+2]
        zvalid = zheader[0] & 15 == 8 and zheader[0] >> 4 <= 7 and int.from_bytes(zheader, 'big') % 31 == 0
        if not (gzip or zvalid):
            continue
        try:
            decoder = zlib.decompressobj(31 if gzip else 15)
            output = decoder.decompress(blob[offset:], 1024 * 1024)
            if decoder.eof:
                compressed.append({'offset': offset, 'format': 'gzip' if gzip else 'zlib', 'decodedBytes': len(output)})
        except zlib.error:
            pass
    candidates = []
    for width in (2, 4):
        for endian in ('little', 'big'):
            for inclusive in (True, False):
                for start in range(min(64, len(blob))):
                    cursor, count = start, 0
                    while cursor + width <= len(blob):
                        length = int.from_bytes(blob[cursor:cursor+width], endian)
                        step = length if inclusive else length + width
                        if step < width + 1 or step > 65536 or cursor + step > len(blob):
                            break
                        cursor += step
                        count += 1
                    if count >= 4:
                        candidates.append({'offset': start, 'width': width, 'endian': endian, 'lengthIncludesHeader': inclusive, 'consecutiveRecords': count, 'bytesCovered': cursor-start})
    return {'bytes': len(blob), 'entropyBitsPerByte': round(entropy(blob), 3),
            'printableAsciiFraction': round(sum(32 <= x <= 126 for x in blob)/len(blob), 3),
            'validatedCompressedStreams': compressed, 'simpleLengthFramingCandidates': candidates}


def main():
    root = pathlib.Path(sys.argv[1]).resolve()
    with contextlib.redirect_stdout(io.StringIO()):
        captured = runpy.run_path(str(pathlib.Path(__file__).with_name('Analyze-KathanaCapture.py')))
    flows = collections.defaultdict(list)
    for segment in captured['segments']:
        flows[segment['flow']].append(segment)
    out = root / 'reassembled'
    out.mkdir(exist_ok=True)
    results = []
    for flow, segments in flows.items():
        direction = 'client-to-server' if flow[3] == 40001 else 'server-to-client'
        anchor, runs, gaps, overlap, conflicts = reassemble(segments)
        result = {'direction': direction, 'flow': flow, 'sequenceAnchor': anchor, 'dataSegments': len(segments),
                  'gapBytes': gaps, 'overlapBytes': overlap, 'conflictingBytes': conflicts, 'runs': []}
        for index, (offset, blob) in enumerate(runs):
            filename = f'{direction}-{flow[1]}-{flow[3]}-run{index}.bin'
            (out / filename).write_bytes(blob)
            result['runs'].append({'file': filename, 'relativeSequenceOffset': offset, **inspect(blob)})
        # These are observed TCP payload starts, explicitly not proven message boundaries.
        result['commonSegmentStartTwoBytes'] = collections.Counter(s['payload'][:2].hex() for s in segments).most_common(8)
        result['segmentTimeline'] = [{'utcUnixSeconds': s['timestamp'], 'phase': s['phase'], 'sequence': s['seq'], 'bytes': len(s['payload'])} for s in segments]
        results.append(result)
    (out / 'stream-analysis.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
    lines = ['# TCP stream analysis', '', 'Capture began mid-connection. Reassembly covers observed bytes only; initial login/handshake bytes are absent. Separate directions and gaps are never concatenated together.', '', '| Direction | Unique observed bytes | Contiguous runs | Missing bytes between runs | Conflicting overlap bytes | Entropy (bits/byte) |', '| --- | ---: | ---: | ---: | ---: | --- |']
    for r in results:
        entropies = [x['entropyBitsPerByte'] for x in r['runs']]
        lines.append(f"| {r['direction']} | {sum(x['bytes'] for x in r['runs']):,} | {len(r['runs'])} | {sum(r['gapBytes']):,} | {r['conflictingBytes']} | {min(entropies):.3f}–{max(entropies):.3f} |")
    lines += ['', '## Structure checks', '']
    for r in results:
        compression = sum(len(x['validatedCompressedStreams']) for x in r['runs'])
        framing = sum(len(x['simpleLengthFramingCandidates']) for x in r['runs'])
        lines.append(f"- {r['direction']}: {compression} validated gzip/zlib streams; {framing} simple length-prefix candidates with at least four consecutive complete records. Frequent first two bytes at TCP payload starts: {r['commonSegmentStartTwoBytes']}.")
    lines += ['', 'Framing candidates are unvalidated heuristic hits, including overlapping and potentially accidental interpretations. None establishes a message boundary. Entropy ranges above are per contiguous run; many runs are very short, which biases empirical entropy downward.', '', 'No message format or command meanings are established by these checks. Length-prefix checks cover 2/4-byte little/big-endian lengths at the first 64 offsets of each run; they do not exhaust all possible framing formats. No validated gzip/zlib result does not exclude raw or custom compression. Entropy cannot distinguish encryption from compression or structured binary encoding. Recurring prefixes are evidence of repetition, not proof of unencrypted gameplay fields.', '', 'Binary streams stay local in this directory. The JSON includes sequence ranges and segment timestamps for comparison against future controlled recordings. Actual action-to-message mapping requires repeatable input observations, not phase labels alone.']
    (out / 'stream-analysis.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
