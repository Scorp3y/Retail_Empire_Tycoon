using RetailEmpireTycoon.BuildSystem;

internal static class DoorCollectionDesigns
{
    internal sealed class Style
    {
        public readonly int Number, Cells, Depth, Price;
        public readonly string Key, English, Russian, EnglishDescription, RussianDescription;
        public readonly DoorMechanism Motion;
        public string Id => $"Door_{Number:00}_{Key}";
        public bool Vehicle => Number >= 26;
        public bool CompactGate => Number is 27 or 28 or 30;
        public bool Powered => Number >= 16;

        public Style(int number, string key, string english, string russian, string description, string translation,
            DoorMechanism motion, int cells, int depth, int price)
        {
            Number = number; Key = key; English = english; Russian = russian;
            EnglishDescription = description; RussianDescription = translation;
            Motion = motion; Cells = cells; Depth = depth; Price = price;
        }
    }

    public static readonly Style[] All =
    {
        new(1,"OakSwing","Oak Swing","Распашная дубовая","Panelled oak leaf with side hinges.","Дубовая филёнчатая створка на боковых петлях.",DoorMechanism.Swing,4,1,100),
        new(2,"GlassSwing","Glass Swing","Распашная стеклянная","Glazed leaf with a long pull handle.","Остеклённая створка с длинной ручкой.",DoorMechanism.Swing,4,1,180),
        new(3,"DoubleSwing","Double Swing","Двустворчатая распашная","Paired cream glazed leaves with pull handles.","Две кремовые остеклённые створки с ручками.",DoorMechanism.Swing,7,1,180),
        new(4,"HalfLite","Half-Lite","Распашная с окном","Cream and sage panel with an upper window.","Кремово-шалфейная дверь с верхним окном.",DoorMechanism.Swing,4,1,100),
        new(5,"ServiceSteel","Service Steel","Стальная служебная","Solid steel service door with a panic bar.","Глухая стальная дверь с нажимной штангой.",DoorMechanism.Swing,4,1,100),
        new(8,"PushThrough","Push-Through","Маятниковая служебная","Double-acting terracotta leaves with portholes.","Терракотовые маятниковые створки с иллюминаторами.",DoorMechanism.Swing,7,1,100),
        new(11,"BarnSlide","Barn Slide","Амбарная сдвижная","Braced oak leaf on an exposed black track.","Дубовая створка с крестовиной на открытой рельсе.",DoorMechanism.Slide,5,1,180),
        new(12,"PocketSlide","Pocket Slide","Сдвижная в карман","Sage leaf disappears into a cream wall pocket.","Шалфейная створка уходит в кремовый карман.",DoorMechanism.Slide,9,1,180),
        new(14,"BiFold","Bi-Fold","Складная двухсекционная","Two cream panels fold together to one side.","Две кремовые панели складываются к краю.",DoorMechanism.Fold,5,1,100),
        new(15,"Accordion","Accordion","Дверь-гармошка","Six narrow oak panels stack at the jamb.","Шесть дубовых секций складываются гармошкой.",DoorMechanism.Fold,6,1,100),
        new(16,"AutoSingle","Auto Single","Автоматическая одностворчатая","One glazed slider parks behind a fixed panel.","Одна стеклянная створка уходит за неподвижную панель.",DoorMechanism.Slide,9,1,180),
        new(17,"AutoDouble","Auto Double","Автоматическая двустворчатая","Two glazed leaves part at the centre.","Две стеклянные створки расходятся от центра.",DoorMechanism.Slide,11,1,320),
        new(18,"Telescopic","Telescopic","Телескопическая","Glazed leaves stack on staggered rails.","Стеклянные створки собираются пакетом на параллельных рельсах.",DoorMechanism.Slide,11,1,320),
        new(19,"CornerSlide","Corner Slide","Угловая раздвижная","Two glazed sliders meet at a right-angle corner.","Две стеклянные створки сходятся на прямом углу.",DoorMechanism.Slide,9,9,320),
        new(20,"CurvedSlide","Curved Slide","Дуговая раздвижная","Two glazed sliders follow a curved guide.","Две стеклянные створки движутся по дуге.",DoorMechanism.Arc,9,5,320),
        new(21,"ThreeWingRevolve","Three-Wing Revolve","Трёхлопастная карусельная","Three glazed wings rotate in a round entry drum.","Три стеклянные лопасти вращаются в круглом тамбуре.",DoorMechanism.Revolve,9,9,320),
        new(22,"FourWingRevolve","Four-Wing Revolve","Четырёхлопастная карусельная","Four glazed wings under a sage canopy.","Четыре стеклянные лопасти под шалфейным куполом.",DoorMechanism.Revolve,11,11,320),
        new(23,"AutoSwing","Auto Swing","Автоматическая распашная","Paired glazed doors with visible powered arms.","Две стеклянные створки с видимыми тягами привода.",DoorMechanism.Swing,7,1,180),
        new(24,"FoldingAuto","Folding Auto","Автоматическая складная","Two glazed folding pairs clear the opening.","Две пары стеклянных створок складываются к краям.",DoorMechanism.Fold,8,1,320),
        new(26,"VerticalLift","Vertical Lift","Вертикальные подъёмные","Rigid green panel rises in external steel guides.","Зелёный щит поднимается по внешним стальным стойкам.",DoorMechanism.Lift,21,2,180),
        new(27,"RollUp","Roll-Up","Рулонные ворота","Steel slats wind around an overhead drum.","Стальные ламели наматываются на верхний барабан.",DoorMechanism.Roll,14,2,100),
        new(28,"Sectional","Sectional","Секционные подъёмные","Cream panels follow curved ceiling tracks.","Кремовые секции уходят по изогнутым потолочным рельсам.",DoorMechanism.Sectional,14,2,180),
        new(29,"TiltUp","Tilt-Up","Откидные ворота","An amber panel tilts out into a canopy.","Янтарная панель откидывается наружу и образует козырёк.",DoorMechanism.Tilt,21,2,180),
        new(30,"SideSectional","Side-Sectional","Боковые секционные","Green sections turn and park along the side wall.","Зелёные секции поворачивают и уходят вдоль боковой стены.",DoorMechanism.SideSectional,14,2,180)
    };
}
