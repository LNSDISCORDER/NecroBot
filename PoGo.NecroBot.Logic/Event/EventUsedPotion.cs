using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Event
{
    public class EventUsedPotion : IEvent
    {
        public string Type;
        public string PokemonId;
        public int PokemonCp;
        public int Remaining;
    }
}
