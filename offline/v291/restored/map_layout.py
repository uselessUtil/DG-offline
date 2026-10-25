"""Recovered map atlas contract: native PNG is top-down; dgdata is bottom-up."""
from PIL import Image
def canonical_map(image,native=False):
    image=image.convert('RGBA')
    assert image.size==(1024,1024),image.size
    return image if native else image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
def separate_panels(image):
    # Empty atlas space is often opaque black. Alpha alone is not evidence.
    return sum(a>0 and max(r,g,b)>8 for r,g,b,a in image.crop((580,320,1024,544)).getdata())>1000
def transparent_black(image):
    image=image.convert('RGBA')
    image.putdata([(r,g,b,0 if r==g==b==0 else a) for r,g,b,a in image.getdata()])
    return image
def parts(image):
    separated=separate_panels(image)
    upper=image.crop((0,0,1024,316))
    if separated:
        normal=image.crop((592,320,776,544));boss=image.crop((812,320,1000,544))
    else:
        normal=image.crop((420,104,604,316));boss=normal.copy()
        upper.paste((0,0,0,0),(420,104,604,316))
    return {'lower':transparent_black(image.crop((0,320,560,1024))),
            'upper':transparent_black(upper),'normal':transparent_black(normal),'boss':transparent_black(boss)},separated
