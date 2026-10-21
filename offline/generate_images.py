"""Generate clearly numbered substitutes for absent downloaded artwork, in Docker."""
import base64
import io
import json
from pathlib import Path
import sys
from PIL import Image, ImageDraw

DIGITS = ['111101101101111','010110010010111','111001111100111',
          '111001111001111','101101111001001','111100111001111',
          '111100111101111','111001001001001','111101111101111','111101111001111']
COLORS = ['#9999a5','#e54833','#338cf2','#f2cc33','#b259d9','#40cc73']


def create(output: Path, tables: Path):
    chars = json.loads((tables / 'MasterDataParamChara.json').read_bytes(), strict=False)
    elements = {int(c['fix_id']): max(0, min(5, int(c['element']) - 1)) for c in chars}
    atlas = Image.new('RGBA', (1024, 1024))
    portraits = []
    for number in range(1024):
        image = Image.new('RGBA', (64, 64), COLORS[elements.get(number, number % 6)])
        draw = ImageDraw.Draw(image)
        draw.rectangle((0, 0, 63, 63), outline='white', width=3)
        label = str(number)
        start = (64 - len(label) * 8) // 2
        for i, digit in enumerate(label):
            for y in range(5):
                for x in range(3):
                    if DIGITS[int(digit)][y * 3 + x] == '1':
                        draw.rectangle((start+i*8+x*2, 25+y*2, start+i*8+x*2+1, 26+y*2), fill='black')
        atlas.paste(image.resize((32, 32), Image.Resampling.NEAREST), (number % 32 * 32, number // 32 * 32))
        buffer = io.BytesIO()
        image.save(buffer, format='PNG')
        portraits.append(base64.b64encode(buffer.getvalue()).decode())
    buffer = io.BytesIO()
    atlas.save(buffer, format='PNG')
    output.write_text('internal static class OfflineImages {\n public const string Atlas = "' +
                      base64.b64encode(buffer.getvalue()).decode() + '";\n' +
                      ' public static readonly string[] Portraits = new string[] {\n' +
                      ',\n'.join('"' + p + '"' for p in portraits) + '\n};\n}\n')


if __name__ == '__main__':
    create(Path(sys.argv[1]), Path(sys.argv[2]))
