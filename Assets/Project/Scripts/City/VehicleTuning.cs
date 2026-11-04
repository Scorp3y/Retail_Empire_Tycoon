using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public enum WorkshopCategory { Engine, Handling, Cargo, Paint }

    [Serializable]
    public struct VehicleTuning
    {
        public const int MaxLevel = 3;
        public int engine, handling, cargo;
        public bool painted;
        public int rgb;

        public Color32? Paint => painted ? new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255) : (Color32?)null;
        public float SpeedMultiplier => 1 + engine * .08f;
        public float AccelerationMultiplier => 1 + engine * .10f;
        public float SteeringMultiplier => 1 + handling * .04f;
        public float CapacityMultiplier => 1 + cargo * .15f;

        public int Level(WorkshopCategory category) => category switch
        {
            WorkshopCategory.Engine => engine,
            WorkshopCategory.Handling => handling,
            WorkshopCategory.Cargo => cargo,
            _ => MaxLevel
        };

        public VehicleTuning Upgrade(WorkshopCategory category)
        {
            var next = this;
            if (category == WorkshopCategory.Engine) next.engine++;
            if (category == WorkshopCategory.Handling) next.handling++;
            if (category == WorkshopCategory.Cargo) next.cargo++;
            return next;
        }

        public VehicleTuning WithPaint(Color32? color)
        {
            var next = this;
            next.painted = color.HasValue;
            var value = color.GetValueOrDefault();
            next.rgb = (value.r << 16) | (value.g << 8) | value.b;
            return next;
        }

        public VehicleTuning Normalized()
        {
            var copy = this;
            copy.engine = Mathf.Clamp(engine, 0, MaxLevel);
            copy.handling = Mathf.Clamp(handling, 0, MaxLevel);
            copy.cargo = Mathf.Clamp(cargo, 0, MaxLevel);
            copy.rgb = Mathf.Clamp(rgb, 0, 0xFFFFFF);
            return copy;
        }
    }

    [Serializable]
    public sealed class VehicleTuningSaveEntry
    {
        public string id;
        public VehicleTuning tuning;
    }
}
