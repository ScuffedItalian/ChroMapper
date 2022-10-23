using System;
using System.Linq;
using Beatmap.Base;
using SimpleJSON;

namespace Beatmap.V2
{
    public class V2Obstacle : BaseObstacle
    {
        private int type;

        public V2Obstacle()
        {
        }

        public V2Obstacle(BaseObstacle other) : base(other) => ParseCustom();

        public V2Obstacle(JSONNode node)
        {
            Time = RetrieveRequiredNode(node, "_time").AsFloat;
            PosX = RetrieveRequiredNode(node, "_lineIndex").AsInt;
            PosY = node["_lineLayer"] ?? 0;
            Type = RetrieveRequiredNode(node, "_type").AsInt;
            Duration = RetrieveRequiredNode(node, "_duration").AsFloat;
            Width = RetrieveRequiredNode(node, "_width").AsInt;
            Height = node["_height"] ?? 0;
            CustomData = node["_customData"];
            InferPosYHeight();
            ParseCustom();
        }

        public V2Obstacle(float time, int posX, int type, float duration, int width, JSONNode customData = null) : base(
            time, posX, type, duration, width, customData) =>
            ParseCustom();

        public override int Type
        {
            get => type;
            set
            {
                type = value;
                InferPosYHeight();
            }
        }

        public override string CustomKeyTrack { get; } = "_track";

        public override string CustomKeyColor { get; } = "_color";

        public override string CustomKeyCoordinate { get; } = "_position";

        public override string CustomKeyWorldRotation { get; } = "_rotation";

        public override string CustomKeyLocalRotation { get; } = "_localRotation";

        public override string CustomKeySize { get; } = "_scale";

        protected sealed override void ParseCustom() => base.ParseCustom();

        public override bool IsChroma() => 
            CustomData != null && CustomData.HasKey("_color") && CustomData["_color"].IsArray;


        public override bool IsNoodleExtensions() =>
            CustomData != null &&
            ((CustomData.HasKey("_animation") && CustomData["_animation"].IsArray) ||
             (CustomData.HasKey("_fake") && CustomData["_fake"].IsBoolean) ||
             (CustomData.HasKey("_interactable") && CustomData["_interactable"].IsBoolean) ||
             (CustomData.HasKey("_localRotation") && CustomData["_localRotation"].IsArray) ||
             (CustomData.HasKey("_noteJumpMovementSpeed") && CustomData["_noteJumpMovementSpeed"].IsNumber) ||
             (CustomData.HasKey("_noteJumpStartBeatOffset") &&
              CustomData["_noteJumpStartBeatOffset"].IsNumber) ||
             (CustomData.HasKey("_position") && CustomData["_position"].IsArray) ||
             (CustomData.HasKey("_rotation") &&
              (CustomData["_rotation"].IsArray || CustomData["_rotation"].IsNumber)) ||
             (CustomData.HasKey("_scale") && CustomData["_scale"].IsArray) ||
             (CustomData.HasKey("_track") && CustomData["_track"].IsString));

        public override bool IsMappingExtensions() =>
            (Width >= 1000 || Type >= 1000 || PosX < 0 || PosX > 3) &&
            !IsNoodleExtensions();

        public override JSONNode ToJson()
        {
            JSONNode node = new JSONObject();
            node["_time"] = Math.Round(Time, DecimalPrecision);
            node["_lineIndex"] = PosX;
            node["_lineLayer"] = PosY;
            node["_type"] = Type;
            node["_duration"] = Math.Round(Duration, DecimalPrecision); //Get rid of float precision errors
            node["_width"] = Width;
            node["_height"] = Height;
            CustomData = SaveCustom();
            if (!CustomData.Children.Any()) return node;
            node["_customData"] = CustomData;
            return node;
        }

        public override BaseItem Clone() => new V2Obstacle(Time, PosX, Type, Duration, Width, SaveCustom().Clone());
    }
}
