﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Event
{
    public class EventGymDeployed : IEvent
    {
        public string PokemonId;
        public int PokemonCp;
        public string Gym;
    }
}
