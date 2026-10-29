using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    public enum ShopIcon { Cart, Warehouse, Settings, Territory, OpenLock, ClosedLock, Staff, Equipment, Products, Back, Close, Check, Sound, Save, Exit, Cashier, Guard, Stocker, Cleaner, Star, Coin, Pause, Play, Pickup, Decoration,
        Hammer, Move, RotateLeft, RotateRight, ArrowUp, ArrowDown, Floor, Ceiling, Erase, Parking }

    /// <summary>Navigation symbols share a normalized silhouette and scale without blurry bitmap resizing.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShopIconGraphic : MaskableGraphic
    {
        [SerializeField] private ShopIcon icon;
        public ShopIcon Icon { get => icon; set { if (icon == value) return; icon = value; SetVerticesDirty(); } }
        private VertexHelper _mesh;
        private Rect _rect;
        private float _lockProgress = -1;
        public float LockProgress
        {
            get => _lockProgress;
            set { float progress = Mathf.Clamp01(value); if (_lockProgress == progress) return; _lockProgress = progress; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); _mesh = mesh; _rect = GetPixelAdjustedRect();
            switch (icon)
            {
                case ShopIcon.Hammer:
                    ConstructionHammer();break;
                case ShopIcon.Move:
                    Line(.15f,.5f,.85f,.5f);Line(.5f,.15f,.5f,.85f);
                    Line(.15f,.5f,.30f,.65f);Line(.15f,.5f,.30f,.35f);Line(.85f,.5f,.70f,.65f);Line(.85f,.5f,.70f,.35f);
                    Line(.5f,.85f,.35f,.70f);Line(.5f,.85f,.65f,.70f);Line(.5f,.15f,.35f,.30f);Line(.5f,.15f,.65f,.30f);break;
                case ShopIcon.RotateLeft: case ShopIcon.RotateRight:
                    float flip=icon==ShopIcon.RotateLeft?1:-1;
                    for(int i=0;i<24;i++)
                    {
                        float a=(40+i*260/24f)*Mathf.Deg2Rad,b=(40+(i+1)*260/24f)*Mathf.Deg2Rad;
                        Line(.5f+flip*Mathf.Cos(a)*.31f,.5f+Mathf.Sin(a)*.31f,.5f+flip*Mathf.Cos(b)*.31f,.5f+Mathf.Sin(b)*.31f,.065f);
                    }
                    Polygon(new Vector2(.5f+flip*.20f,.83f),new Vector2(.5f+flip*.37f,.59f),new Vector2(.5f+flip*.05f,.65f));break;
                case ShopIcon.ArrowUp: case ShopIcon.ArrowDown:
                    float direction=icon==ShopIcon.ArrowUp?1:-1;
                    Line(.24f,.5f-direction*.12f,.5f,.5f+direction*.14f,.09f);Line(.5f,.5f+direction*.14f,.76f,.5f-direction*.12f,.09f);break;
                case ShopIcon.Floor: case ShopIcon.Ceiling:
                    Polygon(new Vector2(.12f,.42f),new Vector2(.5f,.19f),new Vector2(.88f,.42f),new Vector2(.5f,.65f));
                    Line(.29f,.32f,.67f,.55f,.026f,ShopUiTheme.Paper);Line(.31f,.54f,.69f,.31f,.026f,ShopUiTheme.Paper);
                    if(icon==ShopIcon.Ceiling){Line(.18f,.81f,.82f,.81f,.065f);Line(.22f,.81f,.22f,.62f,.05f);Line(.78f,.81f,.78f,.62f,.05f);}break;
                case ShopIcon.Erase:
                    Polygon(new Vector2(.14f,.37f),new Vector2(.55f,.84f),new Vector2(.87f,.56f),new Vector2(.49f,.16f),new Vector2(.35f,.16f));
                    Line(.3f,.37f,.51f,.18f,.035f,ShopUiTheme.Paper);Line(.34f,.12f,.85f,.12f,.045f);break;
                case ShopIcon.Parking:
                    Line(.2f,.17f,.2f,.84f,.065f);Line(.2f,.84f,.82f,.84f,.065f);Line(.82f,.84f,.82f,.17f,.065f);
                    Box(.39f,.28f,.065f,.4f);Box(.43f,.62f,.21f,.065f);Box(.59f,.48f,.065f,.18f);Box(.43f,.46f,.21f,.065f);break;
                case ShopIcon.Pickup:
                    Box(.08f,.32f,.84f,.24f);
                    Polygon(new Vector2(.48f,.56f),new Vector2(.48f,.77f),new Vector2(.7f,.77f),new Vector2(.84f,.56f));
                    Box(.52f,.58f,.15f,.13f,ShopUiTheme.Paper);
                    Circle(.25f,.28f,.105f);Circle(.75f,.28f,.105f);Circle(.25f,.28f,.05f,ShopUiTheme.Paper);Circle(.75f,.28f,.05f,ShopUiTheme.Paper);break;
                case ShopIcon.Decoration:
                    Polygon(new Vector2(.32f,.17f),new Vector2(.68f,.17f),new Vector2(.75f,.43f),new Vector2(.25f,.43f));
                    Line(.5f,.43f,.5f,.78f,.045f);
                    Polygon(new Vector2(.48f,.58f),new Vector2(.22f,.63f),new Vector2(.16f,.84f),new Vector2(.4f,.81f));
                    Polygon(new Vector2(.52f,.68f),new Vector2(.65f,.88f),new Vector2(.87f,.86f),new Vector2(.8f,.66f));break;
                case ShopIcon.Cart:
                    Line(.12f,.78f,.28f,.78f); Line(.28f,.78f,.38f,.3f);
                    Polygon(new Vector2(.31f,.68f),new Vector2(.86f,.68f),new Vector2(.76f,.4f),new Vector2(.37f,.4f));
                    Line(.38f,.3f,.77f,.3f); Circle(.43f,.17f,.075f); Circle(.74f,.17f,.075f); break;
                case ShopIcon.Warehouse:
                    Line(.16f,.22f,.16f,.68f); Line(.84f,.22f,.84f,.68f); Line(.16f,.68f,.5f,.9f); Line(.5f,.9f,.84f,.68f);
                    Line(.16f,.22f,.84f,.22f); Box(.31f,.23f,.38f,.39f); Line(.35f,.4f,.65f,.4f,.035f,ShopUiTheme.Paper); Line(.35f,.5f,.65f,.5f,.035f,ShopUiTheme.Paper); break;
                case ShopIcon.Settings:
                    for (int i=0;i<8;i++) { float a=i*Mathf.PI/4; Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a)); Line(.5f+d.x*.23f,.5f+d.y*.23f,.5f+d.x*.38f,.5f+d.y*.38f,.14f); }
                    Ring(.5f,.5f,.29f,.12f); break;
                case ShopIcon.Territory:
                    Polygon(new Vector2(.13f,.23f),new Vector2(.34f,.14f),new Vector2(.34f,.62f),new Vector2(.13f,.71f));
                    Polygon(new Vector2(.37f,.14f),new Vector2(.62f,.23f),new Vector2(.62f,.61f),new Vector2(.37f,.7f));
                    Polygon(new Vector2(.65f,.23f),new Vector2(.87f,.14f),new Vector2(.87f,.63f),new Vector2(.65f,.72f));
                    Ring(.58f,.75f,.17f,.06f); Polygon(new Vector2(.45f,.64f),new Vector2(.71f,.64f),new Vector2(.58f,.43f)); break;
                case ShopIcon.OpenLock: case ShopIcon.ClosedLock:
                    Box(.24f,.15f,.52f,.4f);
                    Shackle(_lockProgress >= 0 ? _lockProgress : icon == ShopIcon.OpenLock ? 1 : 0);
                    Circle(.5f,.36f,.055f,ShopUiTheme.Paper); Line(.5f,.35f,.5f,.26f,.045f,ShopUiTheme.Paper); break;
                case ShopIcon.Staff:
                    Circle(.43f,.71f,.16f); Circle(.72f,.65f,.12f); Box(.59f,.21f,.3f,.28f); Polygon(new Vector2(.16f,.18f),new Vector2(.2f,.5f),new Vector2(.43f,.58f),new Vector2(.67f,.5f),new Vector2(.72f,.18f)); break;
                case ShopIcon.Equipment:
                    EquipmentShelving(); break;
                case ShopIcon.Products:
                    ProductBasket(); break;
                case ShopIcon.Back: Line(.69f,.5f,.27f,.5f); Line(.27f,.5f,.49f,.72f); Line(.27f,.5f,.49f,.28f); break;
                case ShopIcon.Close: Line(.24f,.24f,.76f,.76f); Line(.24f,.76f,.76f,.24f); break;
                case ShopIcon.Check: Line(.17f,.48f,.4f,.25f); Line(.4f,.25f,.84f,.77f); break;
                case ShopIcon.Sound: Box(.16f,.37f,.2f,.27f); Polygon(new Vector2(.32f,.38f),new Vector2(.6f,.19f),new Vector2(.6f,.81f),new Vector2(.32f,.62f)); Line(.74f,.35f,.84f,.5f); Line(.84f,.5f,.74f,.65f); break;
                case ShopIcon.Save: Box(.2f,.17f,.62f,.67f); Box(.32f,.59f,.33f,.18f,ShopUiTheme.Paper); Box(.32f,.22f,.38f,.24f,ShopUiTheme.Paper); break;
                case ShopIcon.Exit: Line(.23f,.18f,.23f,.82f); Line(.23f,.82f,.49f,.82f); Line(.23f,.18f,.49f,.18f); Line(.43f,.5f,.83f,.5f); Line(.83f,.5f,.66f,.68f); Line(.83f,.5f,.66f,.32f); break;
                case ShopIcon.Cashier: Box(.18f,.17f,.65f,.17f); Box(.24f,.37f,.3f,.16f); Box(.58f,.46f,.1f,.25f); Box(.45f,.65f,.38f,.19f); break;
                case ShopIcon.Guard: Polygon(new Vector2(.17f,.78f),new Vector2(.5f,.9f),new Vector2(.83f,.78f),new Vector2(.76f,.36f),new Vector2(.5f,.13f),new Vector2(.24f,.36f)); Line(.35f,.56f,.47f,.42f,.055f,ShopUiTheme.Paper); Line(.47f,.42f,.67f,.68f,.055f,ShopUiTheme.Paper); break;
                case ShopIcon.Stocker: Box(.19f,.19f,.62f,.58f); Line(.2f,.61f,.8f,.61f,.05f,ShopUiTheme.Paper); Line(.5f,.77f,.5f,.45f,.08f,ShopUiTheme.Paper); break;
                case ShopIcon.Cleaner: Line(.34f,.18f,.67f,.86f,.07f); Polygon(new Vector2(.1f,.18f),new Vector2(.27f,.35f),new Vector2(.64f,.19f),new Vector2(.68f,.06f)); break;
                case ShopIcon.Star:
                    var points=new Vector2[10]; for(int i=0;i<10;i++){float a=Mathf.PI*.5f+i*Mathf.PI/5;float radius=i%2==0?.43f:.21f;points[i]=new Vector2(.5f+Mathf.Cos(a)*radius,.5f+Mathf.Sin(a)*radius);} Polygon(points); break;
                case ShopIcon.Coin:
                    Color gold=new Color32(190,128,15,255); Circle(.5f,.5f,.43f,new Color32(242,191,75,255),12); Ring(.5f,.5f,.34f,.035f,false,gold);
                    Box(.47f,.24f,.055f,.52f,gold); Line(.64f,.67f,.36f,.67f,.07f,gold); Line(.36f,.67f,.36f,.5f,.07f,gold); Line(.36f,.5f,.64f,.5f,.07f,gold); Line(.64f,.5f,.64f,.33f,.07f,gold); Line(.64f,.33f,.36f,.33f,.07f,gold); break;
                case ShopIcon.Pause: Box(.27f,.2f,.16f,.6f); Box(.57f,.2f,.16f,.6f); break;
                case ShopIcon.Play: Polygon(new Vector2(.29f,.18f),new Vector2(.29f,.82f),new Vector2(.82f,.5f)); break;
            }
        }
        private void ConstructionHammer()
        {
            Vector2 P(float x,float y)
            {
                float a=-24*Mathf.Deg2Rad;var d=new Vector2(x-.5f,y-.5f);
                return new Vector2(.5f+d.x*Mathf.Cos(a)-d.y*Mathf.Sin(a),.5f+d.x*Mathf.Sin(a)+d.y*Mathf.Cos(a));
            }
            // Broad flat striking head and a single short claw read as a hammer at 20–28 px.
            // Each piece is convex: the icon renderer's centre fan cannot triangulate a concave claw correctly.
            Polygon(P(.415f,.10f),P(.585f,.10f),P(.56f,.70f),P(.44f,.70f));
            Polygon(P(.30f,.64f),P(.73f,.64f),P(.73f,.84f),P(.30f,.84f));
            Polygon(P(.70f,.61f),P(.87f,.61f),P(.87f,.87f),P(.70f,.87f));
            Polygon(P(.30f,.64f),P(.15f,.71f),P(.13f,.85f),P(.30f,.84f));
            Polygon(P(.15f,.71f),P(.09f,.62f),P(.10f,.81f),P(.13f,.85f));
        }
        private void EquipmentShelving()
        {
            // Stocked shelves describe the whole equipment category, not just construction tools.
            Box(.14f, .14f, .075f, .72f);
            Box(.785f, .14f, .075f, .72f);
            Box(.14f, .19f, .72f, .075f);
            Box(.14f, .48f, .72f, .075f);
            Box(.14f, .785f, .72f, .075f);
            Box(.275f, .555f, .18f, .17f);
            Box(.52f, .555f, .2f, .12f);
            Box(.275f, .265f, .15f, .15f);
            Box(.49f, .265f, .23f, .15f);
        }

        private void ProductBasket()
        {
            Line(.28f, .57f, .42f, .82f, .075f);
            Line(.72f, .57f, .58f, .82f, .075f);
            Box(.12f, .53f, .76f, .08f);
            Line(.17f, .54f, .24f, .22f, .075f);
            Line(.83f, .54f, .76f, .22f, .075f);
            Box(.24f, .18f, .52f, .075f);
            // Real gaps keep the basket monochrome on normal, hover and selected backgrounds.
            Box(.335f, .245f, .065f, .29f);
            Box(.4675f, .245f, .065f, .29f);
            Box(.60f, .245f, .065f, .29f);
        }
        private Vector2 Point(Vector2 p) { float size=Mathf.Min(_rect.width,_rect.height); return _rect.center+(p-Vector2.one*.5f)*size; }
        private void Shackle(float progress)
        {
            Vector2 Pose(Vector2 p)
            {
                Vector2 d = p - new Vector2(.5f, .66f);
                float a = progress * 25 * Mathf.Deg2Rad;
                return new Vector2(.5f + d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a),
                    .66f + d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a) + progress * .07f);
            }
            void Stroke(Vector2 a, Vector2 b)
            {
                a = Pose(a); b = Pose(b);
                Line(a.x, a.y, b.x, b.y, .075f);
            }
            Stroke(new Vector2(.28f, .5f), new Vector2(.28f, .66f));
            Stroke(new Vector2(.72f, .5f), new Vector2(.72f, .66f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 24, b = (i + 1) * Mathf.PI / 24;
                Stroke(new Vector2(.5f + Mathf.Cos(a) * .22f, .66f + Mathf.Sin(a) * .22f),
                    new Vector2(.5f + Mathf.Cos(b) * .22f, .66f + Mathf.Sin(b) * .22f));
            }
        }
        private void Polygon(params Vector2[] p) { Polygon(color,p); }
        private void Polygon(Color tint,params Vector2[] p)
        {
            int start=_mesh.currentVertCount; Vector2 center=Vector2.zero; foreach(var v in p)center+=v; center/=p.Length;
            _mesh.AddVert(Point(center),tint,Vector2.zero); foreach(var v in p)_mesh.AddVert(Point(v),tint,Vector2.zero);
            for(int i=0;i<p.Length;i++)_mesh.AddTriangle(start,start+1+i,start+1+(i+1)%p.Length);
        }
        private void Box(float x,float y,float w,float h,Color? tint=null) { Polygon(tint??color,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)); }
        private void Line(float x,float y,float xx,float yy,float width=.09f,Color? tint=null)
        {
            var a=new Vector2(x,y);var b=new Vector2(xx,yy);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;
            Polygon(tint??color,a-n,b-n,b+n,a+n);
        }
        private void Circle(float x,float y,float r,Color? tint=null,int sides=16)
        {
            var p=new Vector2[sides];for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;p[i]=new Vector2(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r);}Polygon(tint??color,p);
        }
        private void Ring(float x,float y,float r,float width,bool half=false,Color? tint=null)
        {
            int sides=20;float sweep=half?Mathf.PI:Mathf.PI*2;
            for(int i=0;i<sides;i++){float a=i*sweep/sides;float b=(i+1)*sweep/sides;Polygon(tint??color,new Vector2(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r),new Vector2(x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r),new Vector2(x+Mathf.Cos(b)*(r-width),y+Mathf.Sin(b)*(r-width)),new Vector2(x+Mathf.Cos(a)*(r-width),y+Mathf.Sin(a)*(r-width)));}
        }
    }
}
