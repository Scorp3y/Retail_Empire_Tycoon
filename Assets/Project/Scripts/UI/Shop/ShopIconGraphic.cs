using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    public enum ShopIcon { Cart, Warehouse, Settings, Territory, OpenLock, ClosedLock, Staff, Equipment, Products, Back, Close, Check, Sound, Save, Exit, Cashier, Guard, Stocker, Cleaner, Star, Coin, Pause, Play }

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
                    Line(.24f,.2f,.65f,.62f,.13f); Polygon(new Vector2(.37f,.81f),new Vector2(.53f,.94f),new Vector2(.83f,.68f),new Vector2(.7f,.53f)); break;
                case ShopIcon.Products:
                    Polygon(new Vector2(.18f,.23f),new Vector2(.82f,.23f),new Vector2(.88f,.54f),new Vector2(.12f,.54f)); Line(.28f,.49f,.42f,.76f,.08f); Line(.72f,.49f,.58f,.76f,.08f);
                    Line(.36f,.28f,.36f,.48f,.035f,ShopUiTheme.Paper); Line(.5f,.28f,.5f,.48f,.035f,ShopUiTheme.Paper); Line(.64f,.28f,.64f,.48f,.035f,ShopUiTheme.Paper); break;
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
