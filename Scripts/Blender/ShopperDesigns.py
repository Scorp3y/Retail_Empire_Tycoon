"""The fifty numbered designs from the approved shopper sheet. No random outfit generation."""
from dataclasses import dataclass, asdict

COLORS = {
    'skin_light':'EBC4A5', 'skin_peach':'F2CEB5', 'skin_tan':'C99D7C',
    'skin_brown':'AE8063', 'skin_dark':'89624E', 'skin_olive':'D3B292',
    'ginger':'BB8057', 'copper':'AE7356', 'brown':'785E4E', 'chestnut':'916C53',
    'black_hair':'4A4B48', 'blonde':'D7BE83', 'ash':'C8B697', 'silver':'BFC6C2', 'white_hair':'DDDED3',
    'navy':'526D83', 'charcoal':'58636A', 'blue':'7197AE', 'denim':'647F8D',
    'teal':'65A399', 'sage':'A0B093', 'olive':'919D73', 'green':'6B927B',
    'mustard':'D5B774', 'yellow':'E4CD8C', 'orange':'D3A176', 'coral':'D6A291',
    'rust':'BD8B73', 'burgundy':'A87880', 'plum':'9A859C', 'lavender':'BBB0CA',
    'pink':'DDC2C2', 'mint':'AAC8B7', 'cream':'E5DDC9', 'ivory':'F0EBDD',
    'gray':'A3ADA9', 'light_gray':'CCD2CC', 'tan':'C5B291', 'dark_brown':'786855',
    'red':'C57F6F', 'black':'35413F', 'sole':'D9D6C9', 'metal':'B9AD8F',
}

def blend_color(color, target, amount):
    """Small cloth/skin variations in the same warm palette, not extra materials."""
    return ''.join(f'{round(int(color[i:i+2],16)*(1-amount)+int(target[i:i+2],16)*amount):02X}' for i in (0,2,4))

for key in tuple(COLORS):
    if key.startswith('skin_'):
        COLORS[key+'_cheek']=blend_color(COLORS[key],'CC8B80',.12)
    elif key in ('navy','blue','denim','teal','sage','olive','green','mustard','yellow','orange','coral',
                 'rust','burgundy','plum','lavender','pink','mint','cream','ivory','gray','light_gray','tan','charcoal'):
        COLORS[key+'_trim']=blend_color(COLORS[key],COLORS['ivory'],.18)

@dataclass(frozen=True)
class ShopperDesign:
    number: int
    name: str
    skin: str
    hair: str
    hairstyle: str
    outfit: str
    top: str
    bottom: str
    shoes: str
    inner: str = 'ivory'
    glasses: str = ''
    hat: str = ''
    beard: str = ''

    @property
    def identity(self): return f'Shopper_{self.number:02}'
    def data(self): return asdict(self) | {'id':self.identity}

DESIGNS = (
    ShopperDesign(1,'Ginger suit','skin_light','ginger','quiff','suit','navy','navy','charcoal', 'ivory'),
    ShopperDesign(2,'Mustard cardigan bob','skin_peach','brown','bob','cardigan','mustard','navy','cream'),
    ShopperDesign(3,'Teal hoodie','skin_dark','black_hair','crop','hoodie','teal','charcoal','ivory'),
    ShopperDesign(4,'Coral tee ponytail','skin_light','blonde','ponytail','skirt','coral','denim','ivory'),
    ShopperDesign(5,'Gray-haired olive vest','skin_light','silver','swept','vest','olive','dark_brown','dark_brown',glasses='square'),
    ShopperDesign(6,'Plum sweater bun','skin_brown','black_hair','bun','sweater','plum','light_gray','cream'),
    ShopperDesign(7,'Navy cap burgundy sweatshirt','skin_peach','brown','crop','sweater','burgundy','denim','charcoal',hat='cap:navy'),
    ShopperDesign(8,'Forest jacket red bob','skin_peach','copper','bob','jacket','green','charcoal','cream'),
    ShopperDesign(9,'Orange jacket curls','skin_dark','black_hair','curly','jacket','orange','charcoal','charcoal'),
    ShopperDesign(10,'Silver bob lavender cardigan','skin_light','silver','bob','skirt_cardigan','lavender','teal','dark_brown'),
    ShopperDesign(11,'Blue overshirt quiff','skin_light','brown','quiff','jacket','blue','tan','dark_brown'),
    ShopperDesign(12,'Turquoise dress curls','skin_dark','black_hair','curly','dress','teal','teal','charcoal'),
    ShopperDesign(13,'Cream hoodie beanie','skin_light','brown','crop','hoodie','cream','olive','charcoal',hat='beanie:chestnut'),
    ShopperDesign(14,'Pink jacket ponytail','skin_peach','chestnut','ponytail','jacket','pink','navy','charcoal'),
    ShopperDesign(15,'Balding burgundy vest','skin_light','silver','balding','vest','burgundy','tan','dark_brown'),
    ShopperDesign(16,'Blonde bob navy blazer','skin_peach','blonde','bob','jacket','navy','gray','charcoal'),
    ShopperDesign(17,'Sage polo black hair','skin_tan','black_hair','sidepart','polo','sage','charcoal','charcoal'),
    ShopperDesign(18,'Rust sweater long hair','skin_olive','black_hair','long','sweater','rust','cream','dark_brown'),
    ShopperDesign(19,'Ginger beard denim jacket','skin_light','ginger','swept','jacket','blue','tan','dark_brown',beard='short'),
    ShopperDesign(20,'Yellow windbreaker white cap','skin_olive','black_hair','ponytail','jacket','yellow','navy','charcoal',hat='cap:ivory'),
    ShopperDesign(21,'Striped tee afro','skin_dark','black_hair','afro','striped_shorts','navy','tan','ivory'),
    ShopperDesign(22,'Blue hoodie copper bun','skin_peach','copper','bun','hoodie','blue','gray','charcoal'),
    ShopperDesign(23,'Silver hair mint sweater','skin_light','white_hair','wavy','skirt','mint','dark_brown','charcoal',glasses='round'),
    ShopperDesign(24,'Red polo shaved head','skin_tan','black_hair','shaved','polo','red','denim','charcoal'),
    ShopperDesign(25,'Mustard raincoat wavy bob','skin_light','brown','wavy','coat','mustard','charcoal','charcoal'),
    ShopperDesign(26,'Gray suit black sidepart','skin_olive','black_hair','sidepart','suit','light_gray','gray','charcoal',inner='teal'),
    ShopperDesign(27,'Orange cardigan black ponytail','skin_dark','black_hair','ponytail','cardigan','orange','navy','charcoal'),
    ShopperDesign(28,'Teal puffer green beanie','skin_light','brown','crop','puffer','teal','dark_brown','charcoal',hat='beanie:olive'),
    ShopperDesign(29,'Burgundy sweater dress','skin_peach','blonde','bob','dress','burgundy','burgundy','charcoal'),
    ShopperDesign(30,'Blue shirt brown hair glasses','skin_light','chestnut','quiff','shirt','blue','charcoal','dark_brown',glasses='square'),
    ShopperDesign(31,'Green stripe cream sweater bob','skin_light','black_hair','bob','stripe','cream','denim','ivory',inner='green'),
    ShopperDesign(32,'Lavender hoodie curly hair','skin_olive','brown','curly','hoodie','lavender','gray','charcoal'),
    ShopperDesign(33,'White mustache blue cap','skin_light','white_hair','balding','jacket','tan','navy','charcoal',hat='cap:blue',beard='mustache'),
    ShopperDesign(34,'Sage long coat black bun','skin_brown','black_hair','bun','coat','sage','cream','charcoal'),
    ShopperDesign(35,'Yellow tee ginger hair','skin_light','ginger','messy','shorts','yellow','charcoal','ivory'),
    ShopperDesign(36,'Teal blouse auburn hair','skin_peach','copper','long','skirt','teal','dark_brown','dark_brown'),
    ShopperDesign(37,'Brick jacket close crop','skin_dark','black_hair','crop','jacket','rust','tan','charcoal'),
    ShopperDesign(38,'Gray cardigan ash bob','skin_peach','ash','bob','cardigan','gray','plum','charcoal',glasses='square'),
    ShopperDesign(39,'Forest sweater shirt collar','skin_light','brown','quiff','collared','green','tan','dark_brown'),
    ShopperDesign(40,'Coral jacket dark ponytail','skin_olive','black_hair','ponytail','jacket','coral','charcoal','ivory'),
    ShopperDesign(41,'Silver bun blue cardigan','skin_light','silver','bun','skirt_cardigan','blue','cream','dark_brown'),
    ShopperDesign(42,'Teal stripe white polo','skin_brown','black_hair','quiff','stripe','ivory','navy','charcoal',inner='teal'),
    ShopperDesign(43,'Olive overalls ginger bob','skin_peach','ginger','bob','overalls','olive','olive','dark_brown'),
    ShopperDesign(44,'Gray hoodie black beanie','skin_light','black_hair','crop','hoodie','gray','charcoal','charcoal',hat='beanie:charcoal'),
    ShopperDesign(45,'Mustard blouse natural curls','skin_dark','black_hair','afro','shirt','mustard','rust','dark_brown'),
    ShopperDesign(46,'Navy cardigan brown beard','skin_olive','brown','swept','cardigan','navy','olive','dark_brown',beard='short'),
    ShopperDesign(47,'Burgundy blazer dark bob','skin_light','brown','bob','skirt_cardigan','burgundy','gray','charcoal'),
    ShopperDesign(48,'Turquoise windbreaker quiff','skin_tan','brown','quiff','jacket','teal','charcoal','charcoal'),
    ShopperDesign(49,'Coral sweater white hair','skin_light','white_hair','swept','sweater','coral','navy','charcoal',glasses='square'),
    ShopperDesign(50,'Rust sweatshirt sandy hair','skin_light','ash','quiff','sweater','rust','olive','dark_brown'),
)
BY_NUMBER = {design.number:design for design in DESIGNS}
assert len(DESIGNS) == 50 and set(BY_NUMBER) == set(range(1,51))
for design in DESIGNS:
    for key in (design.skin,design.hair,design.top,design.bottom,design.shoes,design.inner):
        assert key in COLORS, (design.identity,key)
