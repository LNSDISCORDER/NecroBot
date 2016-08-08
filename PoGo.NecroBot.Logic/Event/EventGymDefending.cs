using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Event
{
    public class EventGymDefending : IEvent
    {
        public string Trainer;
        public int TrainerLevel;
        public string PokemonId;
        public int PokemonCp;
    }
}
