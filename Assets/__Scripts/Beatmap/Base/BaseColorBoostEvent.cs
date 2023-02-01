using Beatmap.Enums;
using SimpleJSON;

namespace Beatmap.Base
{
    public abstract class BaseColorBoostEvent : BaseEvent
    {
        protected BaseColorBoostEvent() => Type = 5;

        protected BaseColorBoostEvent(BaseColorBoostEvent other)
        {
            Time = other.Time;
            Type = 5;
            Toggle = other.Toggle;
            Value = Toggle ? 1 : 0;
            CustomData = other.SaveCustom().Clone();
        }

        protected BaseColorBoostEvent(BaseEvent evt)
        {
            Time = evt.Time;
            Type = 5;
            Toggle = evt.Value == 1;
            Value = Toggle ? 1 : 0;
            CustomData = evt.SaveCustom().Clone();
        }

        protected BaseColorBoostEvent(float time, bool toggle, JSONNode customData = null) : base(time, 5,
            toggle ? 1 : 0, 1, customData) => Toggle = toggle;

        public override ObjectType ObjectType { get; set; } = ObjectType.Event;
        public bool Toggle { get; set; }

        protected override bool IsConflictingWithObjectAtSameTime(BaseObject other, bool deletion = false)
        {
            if (other is BaseColorBoostEvent cbe) return Toggle = cbe.Toggle;
            return false;
        }

        public override void Apply(BaseObject originalData)
        {
            base.Apply(originalData);

            if (originalData is BaseColorBoostEvent boost) Toggle = boost.Toggle;
        }
    }
}