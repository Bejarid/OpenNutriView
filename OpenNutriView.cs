using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Players.Food;
using Eco.Gameplay.Settlements.Culture;
using Eco.Gameplay.Systems;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using Eco.Simulation;

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenNutriView
{
    public class OpenNutriView
    {
        public static class NextFood
        {
            public static LocString FoodItem(Player player, FoodItem food)
            {
                var (stomachNutrients, stomachCalories, foodCalories) = LoadCurrentStomach(player.User.Stomach);

                var (gain, assumedTaste, nutrition) = CalculateGain(food, player.User.Stomach, stomachNutrients, stomachCalories, foodCalories);
                var sb = new LocStringBuilder();
                sb.AppendLine();
                var assumedTasteAstrix = assumedTaste ? "*" : "";
                sb.AppendLineLoc($"Nutrition times tastiness {Text.Info(Text.Num(nutrition))}{assumedTasteAstrix}");
                sb.AppendLineLoc($"This food will provide you {gain.StyledNum()}{assumedTasteAstrix} XP");
                if (assumedTaste) sb.AppendLine(new LocString("* Assumed delicious tasting"));
                return sb.ToLocString();
            }

            private static (Nutrients stomachNutrients, float stomachCalories, Dictionary<Type, float> foodCalories) LoadCurrentStomach(Stomach stomach)
            {
                var stomachNutrients = new Nutrients();
                var stomachCalories = 0f;
                var foodCalories = new Dictionary<Type, float>();
                foreach (var content in stomach.Contents.Where(content => content.Food.Calories > 0))
                {
                    stomachCalories += content.Food.Calories;
                    stomachNutrients += content.Food.Nutrition * content.Food.Calories;
                    foodCalories.AddOrUpdate(content.Food.Type, content.Food.Calories, (old, val) => old + val);
                }
                return (stomachNutrients, stomachCalories, foodCalories);
            }

            private static (float gain, bool assumedTaste, float nutrition) CalculateGain(FoodItem food, Stomach stomach, Nutrients stomachNutrients, float stomachCalories, Dictionary<Type, float> foodCalories)
            {
                foodCalories = new Dictionary<Type, float>(foodCalories);

                if (food.Calories <= 0)
                    return (0, false, 0);
                stomachCalories += food.Calories;
                stomachNutrients += food.Nutrition * food.Calories;
                foodCalories.AddOrUpdate(food.Type, food.Calories, (sum, val) => sum + val);

                if (stomachCalories > 0)
                    stomachNutrients *= 1f / stomachCalories;

                var nutrientTotal = stomachNutrients.NutrientTotal();
                var (varietyMultiplier, _) = FoodVariety.UpdateMult(foodCalories);
                var (tastinessMultiplier, assumedTaste) = TastinessMultiplier(foodCalories, stomach.TasteBuds);
                var (balancedDietMultiplier, _) = stomachNutrients.CalcBalancedDietMult();
                var cravingMultiplier = CravingMultiplier(food, stomach);
                var dinnerPartyMultiplier = DinnerPartyManager.Obj?.MultiplierForUser(stomach.Owner) ?? 1f;

                if (!FeatureConfig.Obj.FoodVarietyMultiplierEnabled)
                    varietyMultiplier = 1;
                if (!FeatureConfig.Obj.FoodTastinessMultiplierEnabled)
                    tastinessMultiplier = 1;

                var subTotal = nutrientTotal * varietyMultiplier * tastinessMultiplier * balancedDietMultiplier * cravingMultiplier * dinnerPartyMultiplier;
                if (subTotal < 0) subTotal = 0;
                var newSkillRate = (subTotal + EcoSim.BaseSkillGainRate) * BalanceConfig.Obj?.SkillGainMultiplier??1;
                var itemTasteMultiplier = !stomach.TasteBuds.FoodToTaste.TryGetValue(food.Type, out ItemTaste itemTaste) || !itemTaste.Discovered ? ItemTaste.TastinessMultiplier[(int)ItemTaste.TastePreference.Delicious] : itemTaste.TastinessMult;
                var gain = newSkillRate - stomach.NutrientSkillRate();
                var nutrition = food.Nutrition.NutrientTotal() * itemTasteMultiplier;
                return (gain, assumedTaste, nutrition);
            }

            private static (float multiplier, bool assumedTaste) TastinessMultiplier(Dictionary<Type, float> foodToCalories, TasteBuds tasteBuds)
            {
                var totalCalories = foodToCalories.Sum(x => x.Value);
                if (totalCalories == 0)
                    return (1, false);

                var num1 = 0.0f;
                var assumedTaste = false;
                foreach (var (food, calories) in foodToCalories.OrderByDescending(x => x.Value))
                {
                    var taste = tasteBuds.FoodToTaste.TryGetValue(food, out ItemTaste value) ? new ItemTaste?(value) : new ItemTaste?();
                    if (taste.HasValue && taste.Value.Discovered)
                        num1 += taste.Value.TastinessMult * calories;
                    else
                    {
                        num1 += ItemTaste.TastinessMultiplier[(int)ItemTaste.TastePreference.Delicious] * calories;
                        assumedTaste = true;
                    }
                }
                return (num1 / totalCalories, assumedTaste);
            }

            private static float CravingMultiplier(FoodItem food, Stomach stomach)
            {
                var (multiplier, _) = stomach.Cravings.GetMult();
                if (stomach.Craving == null || stomach.Craving != food.Type)
                    return multiplier;

                var count = Convert.ToInt32((multiplier - 1) / Cravings.CravingsBoost) + 1;
                return 1 + count * Cravings.CravingsBoost;
            }
        }
    }
}