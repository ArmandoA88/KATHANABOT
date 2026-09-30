"""Offline analysis of this recorder's little-endian, microsecond pcapng capture."""
import collections
import csv
import datetime as dt
import json
import pathlib
import struct
import sys

root = pathlib.Path(sys.argv[1])
phases = list(csv.DictReader(next(root.glob('*/phases.csv')).open(encoding='utf-8-sig')))
for phase in phases:
    phase['start'] = dt.datetime.fromisoformat(phase['startUtc']).timestamp()
    phase['end'] = dt.datetime.fromisoformat(phase['endUtc']).timestamp()
data = (root / 'packets.pcapng').read_bytes()
stats = collections.defaultdict(collections.Counter)
totals = collections.Counter()
endpoints = collections.Counter()
prefixes = collections.Counter()
lengths = collections.defaultdict(collections.Counter)
seen = set()
segments = []
stamps = []
offset = 0
while offset < len(data):
    kind, size = struct.unpack_from('<II', data, offset)
    if size < 12 or offset + size > len(data) or struct.unpack_from('<I', data, offset + size - 4)[0] != size:
        raise ValueError('Invalid pcapng block')
    block = data[offset:offset + size]
    offset += size
    if kind == 0x0a0d0d0a and block[8:12] != b'\x4d\x3c\x2b\x1a':
        raise ValueError('Unsupported byte order')
    if kind == 1:
        linktype, _, snaplen = struct.unpack_from('<HHI', block, 8)
        if linktype != 1 or size != 20:
            raise ValueError('This analyzer expects the observed Ethernet-labelled IDB with default timestamp resolution')
    if kind != 6:
        continue
    interface, high, low, captured, wire = struct.unpack_from('<IIIII', block, 8)
    if interface != 0 or captured > size - 32:
        raise ValueError('Unexpected interface or captured length')
    timestamp = ((high << 32) | low) / 1e6
    stamps.append(timestamp)
    frame = block[28:28 + captured]
    totals['packets'] += 1
    if captured != wire:
        totals['truncated'] += 1
    ipoffset = 14
    ether_type = struct.unpack_from('!H', frame, 12)[0]
    while ether_type in (0x8100, 0x88a8):
        ether_type = struct.unpack_from('!H', frame, ipoffset + 2)[0]
        ipoffset += 4
    if ether_type != 0x0800:
        # PktMon's observed IDB says Ethernet, but this NIC supplies 802.11
        # data frames. Validate frame control and SNAP instead of guessing offsets.
        control = struct.unpack_from('<H', frame)[0]
        header = 24 + (6 if control & 0x300 == 0x300 else 0)
        if control & 0x80:
            header += 2
            if control & 0x8000:
                header += 4
        if control & 0x0f == 8 and frame[header:header + 8] == bytes.fromhex('aaaa030000000800'):
            ipoffset = header + 8
            totals['wifi_frames'] += 1
        else:
            totals['unsupported_frames'] += 1
            continue
    if frame[ipoffset] >> 4 != 4:
        raise ValueError('Invalid IPv4 version')
    ihl = (frame[ipoffset] & 15) * 4
    iplen = struct.unpack_from('!H', frame, ipoffset + 2)[0]
    if ihl < 20 or ipoffset + iplen > len(frame):
        raise ValueError('Incomplete IPv4 packet')
    if frame[ipoffset + 9] != 6 or struct.unpack_from('!H', frame, ipoffset + 6)[0] & 0x3fff:
        totals['non_tcp_or_fragment'] += 1
        continue
    src = '.'.join(map(str, frame[ipoffset + 12:ipoffset + 16]))
    dst = '.'.join(map(str, frame[ipoffset + 16:ipoffset + 20]))
    tcp = ipoffset + ihl
    sport, dport, seq, ack = struct.unpack_from('!HHII', frame, tcp)
    tcp_len = (frame[tcp + 12] >> 4) * 4
    if tcp_len < 20 or ihl + tcp_len > iplen:
        raise ValueError('Invalid TCP header')
    payload = frame[tcp + tcp_len:ipoffset + iplen]
    direction = 'out' if dport == 40001 else 'in'
    if 40001 not in (sport, dport):
        raise ValueError('Unexpected port outside capture scope')
    endpoints[(src, sport, dst, dport)] += 1
    phase = next((p['phase'] for p in phases if p['start'] <= timestamp < p['end']), 'outside')
    summary = stats[phase]
    summary[direction + '_packets'] += 1
    summary[direction + '_payload_bytes'] += len(payload)
    if payload:
        segments.append({'flow': (src, sport, dst, dport), 'seq': seq, 'timestamp': timestamp, 'phase': phase, 'payload': payload})
        key = (src, sport, dst, dport, seq, payload)
        summary[direction + '_data_packets'] += 1
        if key not in seen:
            summary[direction + '_unique_data_packets'] += 1
            summary[direction + '_unique_payload_bytes'] += len(payload)
        else:
            totals['repeated_data_segments'] += 1
        seen.add(key)
        prefixes[(direction, payload[:4].hex())] += 1
        lengths[direction][len(payload)] += 1
        if payload[:3] in (b'\x16\x03\x01', b'\x16\x03\x03', b'\x17\x03\x03'):
            totals['tls_like_prefix'] += 1
        if payload.startswith((b'GET ', b'POST ', b'HTTP/')):
            totals['http_like_prefix'] += 1
result = {
    'totals': dict(totals),
    'captureStartUtc': dt.datetime.fromtimestamp(min(stamps), dt.timezone.utc).isoformat(),
    'captureEndUtc': dt.datetime.fromtimestamp(max(stamps), dt.timezone.utc).isoformat(),
    'phases': {k: dict(v) for k, v in stats.items()},
    'endpoints': [{'tuple': list(k), 'packets': v} for k, v in endpoints.items()],
    'commonPayloadLengths': {k: v.most_common(10) for k, v in lengths.items()},
    'commonFourBytePrefixes': [(k, v) for k, v in prefixes.most_common(10)],
    'limitations': 'Exact repeated sequence/payload segments deduplicated; no TCP stream reassembly or application protocol decoding. Prefix checks cannot establish encryption or exclude TLS/HTTP. Phases mark instructions, not verified actions.'
}
(root / 'packet-analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
