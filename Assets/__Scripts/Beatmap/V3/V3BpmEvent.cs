using System;
using System.Linq;
using Beatmap.Base;
using SimpleJSON;
using UnityEngine;

namespace Beatmap.V3
{
    public class V3BpmEvent : BaseBpmEvent
    {
        public V3BpmEvent()
        {
        }

        public V3BpmEvent(BaseBpmEvent other) : base(other)
        {
        }

        public V3BpmEvent(BaseEvent evt) : base(evt)
        {
        }

        public V3BpmEvent(JSONNode node)
        {
            Time = RetrieveRequiredNode(node, "b").AsFloat;
            Bpm = RetrieveRequiredNode(node, "m").AsFloat;
            Type = 100;
            CustomData = node["customData"];
        }

        public V3BpmEvent(float time, float bpm, JSONNode customData = null) : base(time, bpm, customData)
        {
        }

        public override Color? CustomColor
        {
            get => null;
            set { }
        }

        public override string CustomKeyTrack { get; } = "track";
        public override string CustomKeyColor { get; } = "color";

        public override JSONNode ToJson()
        {
            JSONNode node = new JSONObject();
            node["b"] = Math.Round(Time, DecimalPrecision);
            node["m"] = Bpm;
            CustomData = SaveCustom();
            if (!CustomData.Children.Any()) return node;
            node["customData"] = CustomData;
            return node;
        }

        public override BaseItem Clone() => new V3BpmEvent(Time, Bpm, SaveCustom().Clone());
    }
}
