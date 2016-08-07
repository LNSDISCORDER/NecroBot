using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeoCoordinatePortable;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;

namespace PoGo.NecroBot.Logic.Tasks
{
    class FarmGymsTask
    {
        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var distanceFromStart = LocationUtils.CalculateDistanceInMeters(
                session.Settings.DefaultLatitude, session.Settings.DefaultLongitude,
                session.Client.CurrentLatitude, session.Client.CurrentLongitude);

            // Edge case for when the client somehow ends up outside the defined radius
            if (session.LogicSettings.MaxTravelDistanceInMeters != 0 &&
                distanceFromStart > session.LogicSettings.MaxTravelDistanceInMeters)
            {
                Logger.Write(
                    session.Translation.GetTranslation(TranslationString.FarmPokestopsOutsideRadius, distanceFromStart),
                    LogLevel.Warning);

                await session.Navigation.Move(
                    new GeoCoordinate(session.Settings.DefaultLatitude, session.Settings.DefaultLongitude),
                    session.LogicSettings.WalkingSpeedInKilometerPerHour, null, cancellationToken, session.LogicSettings.DisableHumanWalking);
            }

            var gymsList = await GetGyms(session);

            if (gymsList.Count <= 0)
            {
                session.EventDispatcher.Send(new WarnEvent
                {
                    Message = session.Translation.GetTranslation(TranslationString.FarmPokestopsNoUsableFound)
                });
            }

            session.EventDispatcher.Send(new PokeStopListEvent { Forts = gymsList });

            while (gymsList.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();

                //resort
                gymsList =
                    gymsList.OrderBy(
                        i =>
                            LocationUtils.CalculateDistanceInMeters(session.Client.CurrentLatitude,
                                session.Client.CurrentLongitude, i.Latitude, i.Longitude)).ToList();
                var pokeStop = gymsList[0];
                gymsList.RemoveAt(0);

                var distance = LocationUtils.CalculateDistanceInMeters(session.Client.CurrentLatitude,
                    session.Client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
                var fortInfo = await session.Client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);


                session.EventDispatcher.Send(new FortTargetEvent { Type = pokeStop.Type, Name = fortInfo.Name, Distance = distance });
                await session.Navigation.Move(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude),
                    session.LogicSettings.WalkingSpeedInKilometerPerHour, null, cancellationToken, session.LogicSettings.DisableHumanWalking);

                switch (pokeStop.Type)
                {
                    case FortType.Checkpoint:
                        break;

                    case FortType.Gym:
                        await FarmGymsTask.ProcessGym(session, cancellationToken, pokeStop);
                        break;

                    default:
                        break;
                }

            }
        }

        private static async Task<List<FortData>> GetGyms(ISession session)
        {
            var mapObjects = await session.Client.Map.GetMapObjects();

            // Wasn't sure how to make this pretty. Edit as needed.
            var pokeStops = mapObjects.Item1.MapCells.SelectMany(i => i.Forts)
                .Where(
                    i =>
                        (i.Type == FortType.Gym) &&
                        i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime() &&
                        ( // Make sure PokeStop is within max travel distance, unless it's set to 0.
                            LocationUtils.CalculateDistanceInMeters(
                                session.Settings.DefaultLatitude, session.Settings.DefaultLongitude,
                                i.Latitude, i.Longitude) < session.LogicSettings.MaxTravelDistanceInMeters ||
                        session.LogicSettings.MaxTravelDistanceInMeters == 0)
                );

            return pokeStops.ToList();
        }
    

        public static async Task ProcessGym(ISession session, CancellationToken cancellationToken, FortData currentFortData)
        {
            bool ignoreGym = false;
            var gymInfo = await session.Client.Fort.GetGymDetails(currentFortData.Id, currentFortData.Latitude,
                currentFortData.Longitude);

            session.EventDispatcher.Send(new EventGymDiscovered
            {
                Name = gymInfo.Name,
                Count = gymInfo.GymState.Memberships.Count.ToString(),
                Team = gymInfo.GymState.FortData.OwnedByTeam.ToString()
            });

            foreach (var defendingPokemon in gymInfo.GymState.Memberships)
            {
                if (session.Profile.PlayerData.Username.Equals(defendingPokemon.TrainerPublicProfile.Name))
                    ignoreGym = true;

                session.EventDispatcher.Send(new EventGymDefending
                {
                    PokemonId = defendingPokemon.PokemonData.PokemonId.ToString(),
                    PokemonCp = defendingPokemon.PokemonData.Cp,
                    Trainer = defendingPokemon.TrainerPublicProfile.Name,
                    TrainerLevel = defendingPokemon.TrainerPublicProfile.Level
                });
            }
            if (ignoreGym)
                return;

            // Deploy Pokemon To Gym
            if (gymInfo.GymState.FortData.OwnedByTeam == session.Profile.PlayerData.Team ||
                gymInfo.GymState.FortData.OwnedByTeam == TeamColor.Neutral)
            {
                // Gym is owned by our team
                await DeployPokemonTask.Execute(session, cancellationToken, currentFortData);
            }
            else
            {
                // Battle Gym ??
                //await BattleGymTask.Execute(session, cancellationToken, currentFortData);
            }
        }

    }
}
