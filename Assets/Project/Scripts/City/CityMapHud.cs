using System;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.Logistics;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public sealed class CityMapHud
    {
        private readonly ShopUi ui;
        private readonly RectTransform canvas;
        private readonly TownWorldPlaces town;
        private readonly PickupDrive pickup;
        private readonly DeliveryCheckpoint home;
        private readonly PickupCamera camera;
        private readonly Func<bool> mayOpen;
        private readonly TMP_Text guidance;
        private RectTransform panel;
        private CityMapGraphic graphic;
        private DeliveryCheckpoint destination;
        public bool IsOpen => panel!=null;

        public CityMapHud(ShopUi ui,RectTransform canvas,TownWorldPlaces town,PickupDrive pickup,DeliveryCheckpoint home,PickupCamera camera,Func<bool> mayOpen)
        {
            this.ui=ui;this.canvas=canvas;this.town=town;this.pickup=pickup;this.home=home;this.camera=camera;this.mayOpen=mayOpen;
            var button=ui.Button(canvas,"M — Карта города",new Vector2(-220,-24),new Vector2(196,44),Toggle);
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,1);
            guidance=ui.Label(canvas,"",new Vector2(-430,-79),new Vector2(406,55),18);
            guidance.rectTransform.anchorMin=guidance.rectTransform.anchorMax=new Vector2(1,1);
        }
        public void Tick()
        {
            if(Input.GetKeyDown(KeyCode.M))Toggle();
            if(IsOpen&&Input.GetKeyDown(KeyCode.Escape))Close();
            if(graphic!=null)graphic.SetVerticesDirty();
            guidance.text=destination==null?"Выберите поставщика на карте":$"{destination.displayName} • {Vector3.Distance(pickup.transform.position,destination.transform.position):0} м";
        }
        public void Toggle()
        {
            if(IsOpen){Close();return;}if(!mayOpen())return;
            pickup.Stop();pickup.InputEnabled=false;camera.SetInteractionBlocked(true);
            panel=ui.Modal("Карта города",canvas,new Vector2(1070,720));
            ui.Heading(panel,"Карта города","Town map",new Vector2(24,-18),new Vector2(850,48));
            ui.Button(panel,"×",new Vector2(1000,-18),new Vector2(44,44),Close);
            var mapRect=ui.Rect("Road network",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-92),new Vector2(700,555));
            graphic=mapRect.gameObject.AddComponent<CityMapGraphic>();graphic.raycastTarget=false;
            graphic.town=town;graphic.pickup=pickup;graphic.home=home;graphic.destination=destination;
            int row=0;
            foreach(var checkpoint in town.suppliers)
            {
                var selected=checkpoint;
                ui.Button(panel,SupplierRouting.Name(selected.supplier),new Vector2(746,-92-row*57),new Vector2(298,46),()=>Select(selected));row++;
            }
            ui.Button(panel,"Мой магазин",new Vector2(746,-92-row*57),new Vector2(298,46),()=>Select(home));
            ui.Label(panel,"Синий — вы · Жёлтый — поставщики · Красный — магазин.\nКлик по поставщику выбирает ориентир. Управление только из машины.",new Vector2(24,-664),new Vector2(1020,42),16);
        }
        private void Select(DeliveryCheckpoint checkpoint) {destination=checkpoint;Close();}
        private void Close()
        {
            ui.CloseModal(panel);panel=null;graphic=null;pickup.InputEnabled=true;camera.SetInteractionBlocked(false);
        }
    }
}
