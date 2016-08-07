using POGOProtos.Map.Fort;

namespace PoGo.NecroBot.Logic.Event
{
    public class FortTargetEvent : IEvent
    {
        public double Distance;
        public string Name;
        public FortType Type;
    }
}