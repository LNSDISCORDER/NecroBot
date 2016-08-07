using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.State;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Data;
using POGOProtos.Data.Battle;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Responses;

namespace PoGo.NecroBot.Logic.Tasks
{
    class BattleGymTask
    {
        public static async Task Execute(ISession session, CancellationToken cancellationToken, FortData currentFortData)
        {
            bool fighting = true;
            var badassPokemon = await session.Inventory.GetHighestCpForGym(6);

            // Start Battle
            
            var gymInfo =
                await
                    session.Client.Fort.GetGymDetails(currentFortData.Id, currentFortData.Latitude,
                        currentFortData.Longitude);

            while (fighting)
            {
                // Heal pokemon
                foreach (var pokemon in badassPokemon)
                {
                    if (pokemon.Stamina <= 0)
                        await DeployPokemonTask.RevivePokemon(session, pokemon);
                    if (pokemon.Stamina < pokemon.StaminaMax)
                        await DeployPokemonTask.HealPokemon(session, pokemon);
                }

                var result = await StartBattle(session, badassPokemon, currentFortData);
                if (result != null)
                {
                    if (result.Result == StartGymBattleResponse.Types.Result.Success)
                    {
                        switch (result.BattleLog.State)
                        {
                            case BattleState.Active:
                                Debug.WriteLine($"Time to start the Attack Mode");
                                await AttackGym(session, cancellationToken, currentFortData, result);
                                break;
                            case BattleState.Defeated:
                                break;
                            case BattleState.StateUnset:
                                break;
                            case BattleState.TimedOut:
                                break;
                            case BattleState.Victory:
                                fighting = false;
                                break;
                            default:
                                Debug.WriteLine($"Unhandled result starting gym battle:\n{result.ToString()}");
                                break;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Hmmm, no result?");
                        Thread.Sleep(5000);
                    }

                    gymInfo = await
                        session.Client.Fort.GetGymDetails(currentFortData.Id, currentFortData.Latitude,
                        currentFortData.Longitude);
                    if (gymInfo.GymState.FortData.OwnedByTeam == TeamColor.Neutral ||
                        gymInfo.GymState.FortData.OwnedByTeam == session.Profile.PlayerData.Team)
                        break;
                }
            }
            // Finished battling.. OwnedByTeam should be neutral when we reach here
            if (gymInfo.GymState.FortData.OwnedByTeam == TeamColor.Neutral ||
                gymInfo.GymState.FortData.OwnedByTeam == session.Profile.PlayerData.Team)
            {
                await DeployPokemonTask.Execute(session, cancellationToken, currentFortData);
            }
            else
            {
                Debug.WriteLine($"Hmmm, for some reason the gym was not taken over...");
            }
        }

        private static async Task AttackGym(ISession session, CancellationToken cancellationToken, FortData currentFortData, StartGymBattleResponse startResponse)
        {
            long serverMs = startResponse.BattleLog.BattleStartTimestampMs;
            DateTime now = DateTime.Now;
            List<BattleAction> lastActions = new List<BattleAction>();
            lastActions = startResponse.BattleLog.BattleActions.ToList();

            Debug.WriteLine($"Gym battle started; fighting trainer: {startResponse.Defender.TrainerPublicProfile.Name}");
            Debug.WriteLine($"We are attacking: {startResponse.Defender.ActivePokemon.PokemonData.PokemonId}");
            bool useSpecial = false;
            int loops = 0;
            List<BattleAction> emptyActions = new List<BattleAction>();
            BattleAction emptyAction = new BattleAction();

            while (true)
            {
                var attackResult =
                    await session.Client.Fort.AttackGym
                    (
                        currentFortData.Id,
                        startResponse.BattleId,
                        (loops > 0 ? GetBattleActions(serverMs, useSpecial) : emptyActions),
                        (loops > 0 ? lastActions.Last() : emptyAction)
                    );
                loops++;

                if (attackResult.Result == AttackGymResponse.Types.Result.Success)
                {

                    switch (attackResult.BattleLog.State)
                    {
                        case BattleState.Active:
                            Debug.WriteLine(
                                $"Successful attack! - They have {attackResult.ActiveDefender.CurrentHealth} health left, we have {attackResult.ActiveAttacker.CurrentHealth} health, energy: {attackResult.ActiveAttacker.CurrentEnergy}");
                            break;
                        case BattleState.Defeated:
                            Debug.WriteLine(
                                $"We were defeated...");
                            return;
                        case BattleState.TimedOut:
                            Debug.WriteLine(
                                $"Our attack timed out...: {attackResult.ToString()}");
                            return;
                        case BattleState.StateUnset:
                            Debug.WriteLine(
                                $"State was unset?: {attackResult.ToString()}");
                            return;
                        case BattleState.Victory:
                            Debug.WriteLine(
                                $"We were victorious!: {attackResult.ToString()}");
                            return;
                        default:
                            Debug.WriteLine(
                                $"Unhandled attack response: {attackResult.ToString()}");
                            continue;
                    }
                    Debug.WriteLine($"{attackResult.ToString()}");
                    Thread.Sleep(1650);
                }
                else
                {
                    Debug.WriteLine($"Unexpected attack result:\n{attackResult.ToString()}");
                    continue;
                }

                if (attackResult.BattleLog != null && attackResult.BattleLog.BattleActions.Count > 0)
                    lastActions.AddRange(attackResult.BattleLog.BattleActions);
                serverMs = attackResult.BattleLog.ServerMs;
            }
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static DateTime DateTimeFromUnixTimestampMillis(long millis)
        {
            return UnixEpoch.AddMilliseconds(millis);
        }


        public static List<BattleAction> GetBattleActions(long millis, bool useSpecial)
        {
            List<BattleAction> actions = new List<BattleAction>();
            DateTime now = DateTimeFromUnixTimestampMillis(millis);
            for (int x = 0; x < 3; x++)
            {
                BattleAction action = new BattleAction();

                if (x == 2 && useSpecial)
                {
                    now = now.AddMilliseconds(3400);
                    action.Type = BattleActionType.ActionSpecialAttack;
                    action.DurationMs = 3300;
                }
                else
                {
                    now = now.AddMilliseconds(600);
                    action.Type = BattleActionType.ActionAttack;
                    action.DurationMs = 500;
                }


                action.ActionStartMs = now.ToUnixTime();
                action.TargetIndex = -1;

                Debug.WriteLine($"Attack {x} using AttackStartMs: {action.ActionStartMs}");
                actions.Add(action);
            }

            return actions;
        }

        private static async Task<StartGymBattleResponse> StartBattle(ISession session, IEnumerable<PokemonData> pokemons, FortData currentFortData)
        {
            var gymInfo = await session.Client.Fort.GetGymDetails(currentFortData.Id, currentFortData.Latitude, currentFortData.Longitude);
            int trys = 0;

            var result = await session.Client.Fort.StartGymBattle(currentFortData.Id,
                    gymInfo.GymState.Memberships.First().PokemonData.Id,
                    pokemons.Select(pokemon => pokemon.Id));

            while (true)
            {
                trys++;
                if (result.Result == StartGymBattleResponse.Types.Result.Success)
                {
                    switch (result.BattleLog.State)
                    {
                        case BattleState.Active:
                            if (result.Result == StartGymBattleResponse.Types.Result.Success)
                            {
                                Debug.WriteLine($"Battle was started, result: {result.ToString()}");
                                return result;
                            }
                            else
                            {
                                Debug.WriteLine($"Unexpected result from Server: {result.ToString()}");
                            }
                            break;
                        case BattleState.Defeated:
                            Debug.WriteLine($"We were defeated in battle.");
                            return result;
                        case BattleState.Victory:
                            Debug.WriteLine($"We were victorious");
                            return result;
                        case BattleState.StateUnset:
                            Debug.WriteLine($"Error occoured: {result.BattleLog.State}");
                            break;
                        case BattleState.TimedOut:
                            Debug.WriteLine($"Error occoured: {result.BattleLog.State}");
                            break;
                        default:
                            Debug.WriteLine($"Unhandled occoured: {result.BattleLog.State}");
                            break;
                    }
                }
                else if (result.Result == StartGymBattleResponse.Types.Result.ErrorGymBattleLockout)
                {
                    return result;
                }
                else if (result.Result == StartGymBattleResponse.Types.Result.ErrorAllPokemonFainted)
                {
                    return result;
                }
                else
                {
                    Debug.WriteLine($"Unhandled StartGymBattle response:\n{result.ToString()}");
                }

                if (trys > 5)
                    return result;

                result = await session.Client.Fort.StartGymBattle(currentFortData.Id,
                    gymInfo.GymState.Memberships.First().PokemonData.Id,
                    pokemons.Select(pokemon => pokemon.Id));
            }
        }
    }
}
