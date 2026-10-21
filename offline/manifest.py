"""Edit binary Android XML without recompiling any Android resources."""
import struct

U32 = struct.Struct('<I')
NONE = 0xffffffff


def chunks(data, start=8):
    while start < len(data):
        kind, header, size = struct.unpack_from('<HHI', data, start)
        if size < header or size == 0 or start + size > len(data):
            raise ValueError('Invalid Android XML chunk')
        yield kind, bytearray(data[start:start+size])
        start += size


def length(data, pos, utf8):
    if utf8:
        first = data[pos]
        return (((first & 127) << 8) | data[pos+1], pos+2) if first & 128 else (first, pos+1)
    first = struct.unpack_from('<H', data, pos)[0]
    return (((first & 32767) << 16) | struct.unpack_from('<H', data, pos+2)[0], pos+4) if first & 32768 else (first, pos+2)


def encode_length(value, utf8):
    if utf8:
        return bytes([value]) if value < 128 else bytes([128 | (value >> 8), value & 255])
    return struct.pack('<H', value) if value < 32768 else struct.pack('<HH', 32768 | (value >> 16), value & 65535)


def strings_from(chunk):
    count, style_count, flags, start, style_start = struct.unpack_from('<5I', chunk, 8)
    utf8 = bool(flags & 256)
    result = []
    for i in range(count):
        offset = start + U32.unpack_from(chunk, 28 + i*4)[0]
        n, offset = length(chunk, offset, utf8)
        if utf8:
            n, offset = length(chunk, offset, True)
            result.append(bytes(chunk[offset:offset+n]).decode('utf8'))
        else:
            result.append(bytes(chunk[offset:offset+2*n]).decode('utf-16le'))
    return result


def string_pool(original, strings):
    count, styles, flags, _, style_start = struct.unpack_from('<5I', original, 8)
    if styles:
        raise ValueError('Styled manifest strings are unsupported')
    utf8 = bool(flags & 256)
    blob, offsets = bytearray(), []
    for text in strings:
        offsets.append(len(blob))
        raw = text.encode('utf8' if utf8 else 'utf-16le')
        units = len(text.encode('utf-16le')) // 2
        blob += encode_length(units, utf8)
        if utf8:
            blob += encode_length(len(raw), True)
        blob += raw + (b'\0' if utf8 else b'\0\0')
    blob += b'\0' * (-len(blob) % 4)
    start = 28 + 4*len(strings)
    return (struct.pack('<HHI5I', 1, 28, start+len(blob), len(strings), 0, flags & ~1, start, 0) +
            b''.join(U32.pack(o) for o in offsets) + blob)


def attributes(chunk, strings):
    offset, size, count = struct.unpack_from('<HHH', chunk, 24)
    for index in range(count):
        pos = 16 + offset + index*size
        namespace, name, raw = struct.unpack_from('<III', chunk, pos)
        value_type = chunk[pos+15]
        value = U32.unpack_from(chunk, pos+16)[0]
        text = strings[value] if value_type == 3 else None
        yield strings[name], text, pos


def patch(data, package='jp.gungho.dg.offline106', label='Divine Gate Offline 1.0.6',
          extra_permissions=(), extra_components=()):
    items = list(chunks(data))
    pool = next(c for k,c in items if k == 1)
    strings = strings_from(pool)
    if strings.count('jp.gungho.dg') != 1:
        raise ValueError('Unexpected source package')
    strings[strings.index('jp.gungho.dg')] = package
    label_index = len(strings)
    strings.append(label)
    result = []
    skip_depth = 0
    removed = []
    remove_permissions = {'android.permission.INTERNET', 'com.android.vending.BILLING', 'android.permission.ACCESS_NETWORK_STATE'} | set(extra_permissions)
    for kind, chunk in items:
        if kind == 1:
            result.append(string_pool(chunk, strings))
            continue
        if kind == 0x102:
            tag = strings[U32.unpack_from(chunk, 20)[0]]
            attrs = list(attributes(chunk, strings))
            name = next((v for k,v,p in attrs if k == 'name'), '')
            if skip_depth:
                skip_depth += 1
                continue
            if ((tag == 'uses-permission' and name in remove_permissions) or
                (tag in {'service','receiver','activity'} and name in {
                    'com.prime31.billing.BillingService', 'com.prime31.billing.BillingReceiver',
                    'com.prime31.GoogleIABProxyActivity', 'jp.appAdForce.android.InstallReceiver',
                    'jp.gungho.unityplugin.GoeIntentReceiveActivity'} | set(extra_components)) or
                (tag == 'meta-data' and (name.startswith('APPADFORCE_') or
                 name.startswith('com.google.android.gms.') or name == 'com.prime31.PlayGameServicesPlugin'))):
                skip_depth = 1
                removed.append(name)
                continue
            for key, value, pos in attrs:
                if key == 'label':
                    struct.pack_into('<I', chunk, pos+8, label_index)
                    chunk[pos+15] = 3
                    struct.pack_into('<I', chunk, pos+16, label_index)
        elif kind == 0x103 and skip_depth:
            skip_depth -= 1
            continue
        if not skip_depth:
            result.append(chunk)
    if skip_depth:
        raise ValueError('Unbalanced manifest')
    body = b''.join(result)
    return struct.pack('<HHI', 3, 8, len(body)+8) + body, removed
