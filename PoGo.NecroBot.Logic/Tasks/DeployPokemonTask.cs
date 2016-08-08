using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.State;
using POGOProtos.Data;
using POGOProtos.Inventory.Item;
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Responses;

namespace PoGo.NecroBot.Logic.Tasks
{
    class DeployPokemonTask
    {
        public static async Task Execute(ISession session, CancellationToken cancellationToken, FortData currentFortData)
        {
            var pokemons = await session.Inventory.GetHighestCpForGym(3);
            var pokemonToDeploy = pokemons.First();

            // Ensure they are max health
            foreach (var pokemon in pokemons)
            {
                if (pokemon.Stamina <= 0)
                    await RevivePokemon(session, pokemon);
                if (pokemon.Stamina < pokemon.StaminaMax)
                    await HealPokemon(session, pokemon);
            }

            // Deploy to gym
            var deployResponse = await session.Client.Fort.FortDeployPokemon(currentFortData.Id, pokemonToDeploy.Id);
            switch (deployResponse.Result)
            {
                case FortDeployPokemonResponse.Types.Result.Success:
                    Debug.WriteLine($"Deployed Pokemon: {pokemonToDeploy.PokemonId}; CP: {pokemonToDeploy.Cp}");
                    session.EventDispatcher.Send(new EventGymDeployed
                    {
                        Gym = deployResponse.FortDetails.Name,
                        PokemonCp = pokemonToDeploy.Cp,
                        PokemonId = pokemonToDeploy.PokemonId.ToString()
                    });
                    return;
                case FortDeployPokemonResponse.Types.Result.ErrorFortIsFull:
                    Debug.WriteLine($"Gym is full...");
                    return;
                case FortDeployPokemonResponse.Types.Result.ErrorPokemonNotFullHp:
                    Debug.WriteLine($"One of our submitted pokemon does not have full HP...");
                    return;
                case FortDeployPokemonResponse.Types.Result.ErrorAlreadyHasPokemonOnFort:
                    Debug.WriteLine($"We already have pokemon on this gym!");
                    return;
                case FortDeployPokemonResponse.Types.Result.ErrorOpposingTeamOwnsFort:
                    Debug.WriteLine($"Another team owns this gym =\\");
                    return;

                default:
                    Debug.WriteLine($"Unexpected FortDeploy response: {deployResponse.ToString()}");
                    return;
            }


        }

        public static async Task RevivePokemon(ISession session, PokemonData pokemon)
        {
            var normalRevives = await session.Inventory.GetItemAmountByType(ItemId.ItemRevive);
            if (normalRevives > 0 && pokemon.Stamina <= 0)
            {
                var ret = await session.Client.Inventory.UseItemRevive(ItemId.ItemRevive, pokemon.Id);
                switch (ret.Result)
                {
                    case UseItemReviveResponse.Types.Result.Success:
                        session.EventDispatcher.Send(new EventUsedRevive
                        {
                            Type = "normal",
                            PokemonCp = pokemon.Cp,
                            PokemonId = pokemon.PokemonId.ToString(),
                            Remaining = (normalRevives - 1)
                        });
                        break;
                    case UseItemReviveResponse.Types.Result.ErrorDeployedToFort:
                        Debug.WriteLine($"Pokemon: {pokemon.PokemonId} (CP: {pokemon.Cp}) is already deployed to a gym...");
                        return;
                    case UseItemReviveResponse.Types.Result.ErrorCannotUse:
                        return;
                    default:
                        return;
                }

                return;
            }

            var maxRevives = await session.Inventory.GetItemAmountByType(ItemId.ItemMaxRevive);
            if (maxRevives > 0 && pokemon.Stamina <= 0)
            {
                var ret = await session.Client.Inventory.UseItemRevive(ItemId.ItemMaxRevive, pokemon.Id);
                switch (ret.Result)
                {
                    case UseItemReviveResponse.Types.Result.Success:
                        session.EventDispatcher.Send(new EventUsedRevive
                        {
                            Type = "max",
                            PokemonCp = pokemon.Cp,
                            PokemonId = pokemon.PokemonId.ToString(),
                            Remaining = (maxRevives - 1)
                        });
                        break;

                    case UseItemReviveResponse.Types.Result.ErrorDeployedToFort:
                        Debug.WriteLine($"Pokemon: {pokemon.PokemonId} (CP: {pokemon.Cp}) is already deployed to a gym...");
                        return;

                    case UseItemReviveResponse.Types.Result.ErrorCannotUse:
                        return;

                    default:
                        return;
                }
            }
        }

        public static async Task HealPokemon(ISession session, PokemonData pokemon)
        {
            var normalPotions = await session.Inventory.GetItemAmountByType(ItemId.ItemPotion);
            while (normalPotions > 0 && (pokemon.Stamina < pokemon.StaminaMax))
            {
                var ret = await session.Client.Inventory.UseItemPotion(ItemId.ItemPotion, pokemon.Id);
                switch (ret.Result)
                {
                    case UseItemPotionResponse.Types.Result.Success:
                        session.EventDispatcher.Send(new EventUsedPotion
                        {
                            Type = "normal",
                            PokemonCp = pokemon.Cp,
                            PokemonId = pokemon.PokemonId.ToString(),
                            Remaining = (normalPotions - 1)
                        });
                        break;

                    case UseItemPotionResponse.Types.Result.ErrorDeployedToFort:
                        Debug.WriteLine($"Pokemon: {pokemon.PokemonId} (CP: {pokemon.Cp}) is already deployed to a gym...");
                        return;

                    case UseItemPotionResponse.Types.Result.ErrorCannotUse:
                        return;

                    default:
                        return;
                }
                normalPotions--;
            }

            var superPotions = await session.Inventory.GetItemAmountByType(ItemId.ItemSuperPotion);
            while (superPotions > 0 && (pokemon.Stamina < pokemon.StaminaMax))
            {
                var ret = await session.Client.Inventory.UseItemPotion(ItemId.ItemSuperPotion, pokemon.Id);
                switch (ret.Result)
                {
                    case UseItemPotionResponse.Types.Result.Success:
                        session.EventDispatcher.Send(new EventUsedPotion
                        {
                            Type = "super",
                            PokemonCp = pokemon.Cp,
                            PokemonId = pokemon.PokemonId.ToString(),
                            Remaining = (superPotions - 1)
                        });
                        break;

                    case UseItemPotionResponse.Types.Result.ErrorDeployedToFort:
                        Debug.WriteLine($"Pokemon: {pokemon.PokemonId} (CP: {pokemon.Cp}) is already deployed to a gym...");
                        return;

                    case UseItemPotionResponse.Types.Result.ErrorCannotUse:
                        return;

                    default:
                        return;
                }
                superPotions--;
            }

            var hyperPotions = await session.Inventory.GetItemAmountByType(ItemId.ItemHyperPotion);
            while (hyperPotions > 0 && (pokemon.Stamina < pokemon.StaminaMax))
            {
                var ret = await session.Client.Inventory.UseItemPotion(ItemId.ItemHyperPotion, pokemon.Id);
                switch (ret.Result)
                {
                    case UseItemPotionResponse.Types.Result.Success:
                        session.EventDispatcher.Send(new EventUsedPotion
                        {
                            Type = "hyper",
                            PokemonCp = pokemon.Cp,
                            PokemonId = pokemon.PokemonId.ToString(),
                            Remaining = (hyperPotions - 1)
                        });
                        break;

                    case UseItemPotionResponse.Types.Result.ErrorDeployedToFort:
                        Debug.WriteLine($"Pokemon: {pokemon.PokemonId} (CP: {pokemon.Cp}) is already deployed to a gym...");
                        return;

                    case UseItemPotionResponse.Types.Result.ErrorCannotUse:
                        return;

                    default:
                        return;
                }
                hyperPotions--;
            }

            var maxPotions = await session.Inventory.GetItemAmountByType(ItemId.ItemMaxPotion);
            while (maxPotions > 0 && (pokemon.Stamina < pokemon.StaminaMax))
            {
                var ret = await session.Client.Inventory.UseItemPotion(ItemId.ItemMaxPotion, pokemon.Id);
                switch (ret.Result)
                {
                    case UseItemPotionResponse.Types.Result.Success:
                        session.EventDispatcher.Send(new EventUsedPotion
                        {
                            Type = "max",
                            PokemonCp = pokemon.Cp,
                            PokemonId = pokemon.PokemonId.ToString(),
                            Remaining = (maxPotions - 1)
                        });
                        break;

                    case UseItemPotionResponse.Types.Result.ErrorDeployedToFort:
                        Debug.WriteLine($"Pokemon: {pokemon.PokemonId} (CP: {pokemon.Cp}) is already deployed to a gym...");
                        return;

                    case UseItemPotionResponse.Types.Result.ErrorCannotUse:
                        return;

                    default:
                        return;
                }
                maxPotions--;
            }
        }
    }
}
