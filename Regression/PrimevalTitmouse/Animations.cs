﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PrimevalTitmouse
{
    //Added enum to indicate type instead of strings.
    public enum FullnessType
    {
        None,
        Clear,
        Wet,
        Messy,
        WetMessy,
        Drying
    }

    internal static class Animations
    {
        //<FIXME> Adding Leo here as a quick fix to a softlock issue due to not having ABDL dialogue written
        private static readonly List<string> NPC_LIST = new List<string> { "Linus", "Krobus", "Dwarf", "Leo" };
        public static readonly int poopAnimationTime = 1200; //ms
        public static readonly int peeAnimationTime = 600; //ms
        //Magic Constants
        public const string SPRITES = "Assets/sprites.png";
        public const int PAUSE_TIME = 20000;
        public const float DRINK_ANIMATION_INTERVAL = 80f;
        public const int DRINK_ANIMATION_FRAMES = 8;
        public const int LARGE_SPRITE_DIM = 64;
        public const int SMALL_SPRITE_DIM = 16;
        public const int DIAPER_HUD_DIM   = 64;
        enum FaceDirection : int
        {
            Down  = 2,
            Left  = 1,
            Right = 3,
            Up    = 0
        };

        public static Texture2D sprites;
        private static Data t;
        private static Farmer who;

        //Static Accessor Methods. Ensure that variables are initialized.
        public static Data GetData()
        {
            t ??= Regression.t;
            return t;
        }
        public static Texture2D GetSprites()
        {
            sprites ??= Regression.help.ModContent.Load<Texture2D>(SPRITES);
            return sprites;
        }

        public static Farmer GetWho()
        {
            Animations.who ??= Game1.player;
            return who;
        }

        public static float ZoomScale()
        {
            return Game1.options.zoomLevel / Game1.options.uiScale;
        }

        public static void AnimateDrinking(bool waterSource = false)
        {
            //If we aren't facing downward, turn
            if (Animations.GetWho().getFacingDirection() != (int)FaceDirection.Down)
                Animations.GetWho().faceDirection((int)FaceDirection.Down);

            //Stop doing anything that would prevent us from moving
            //Essentially take control of the variable
            Animations.GetWho().forceCanMove();

            //Stop any form of animation
            Animations.GetWho().completelyStopAnimatingOrDoingAction();

            // ISSUE: method pointer
            //Start Drinking animation. While drinking pause time and don't allow movement.
            Animations.GetWho().FarmerSprite.animateOnce(StardewValley.FarmerSprite.drink, DRINK_ANIMATION_INTERVAL, DRINK_ANIMATION_FRAMES, new AnimatedSprite.endOfAnimationBehavior(EndDrinking));
            Animations.GetWho().freezePause = PAUSE_TIME;
            Animations.GetWho().canMove = false;

            //If we drink from the watering can, don't say anything
            if (!waterSource)
                return;

            //Otherwise say something about it
            Say(Animations.GetData().Drink_Water_Source, null);
        }

        //Not really an animation. Just say the bedding's current state.
        public static void AnimateDryingBedding(Body b)
        {
            Write(Animations.GetData().Bedding_Still_Wet, b);
        }


        public static void AnimateMessingStart(Body b, bool voluntary, bool inUnderwear)
        {

            if (b.IsFishing() || !Animations.GetWho().canMove) return;

            if (b.underwear.removable || inUnderwear)
                Game1.playSound("slosh");

            if (b.isSleeping || !voluntary && !Regression.config.AlwaysNoticeAccidents && (double)b.bowelContinence + 0.449999988079071 <= Regression.rnd.NextDouble())
                return;

            if (!(b.underwear.removable || inUnderwear))
            {
                Animations.Say(Animations.GetData().Cant_Remove, b);
                return;
            }

            if (!inUnderwear)
            {
                if (b.InToilet())
                    Say(Animations.GetData().Poop_Toilet, b);
                else
                    Say(Animations.GetData().Poop_Voluntary, b);
            }
            else if (voluntary)
                Say(Animations.GetData().Mess_Voluntary, b);
            else
                Say(Animations.GetData().Mess_Accident, b);

            //Animations.GetWho().forceCanMove();
            //Animations.GetWho().completelyStopAnimatingOrDoingAction();
            Animations.GetWho().jitterStrength = 1.0f;
            Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Microsoft.Xna.Framework.Rectangle(192, 1152, Game1.tileSize, Game1.tileSize), 50f, 4, 0, Animations.GetWho().position.Value - new Vector2(((Character)Animations.GetWho()).facingDirection.Value == 1 ? 0.0f : (float)-Game1.tileSize, (float)(Game1.tileSize * 2)), false, ((Character)Animations.GetWho()).facingDirection.Value == 1, (float)((Character)Animations.GetWho()).StandingPixel.Y / 10000f, 0.01f, Microsoft.Xna.Framework.Color.White, 1f, 0.0f, 0.0f, 0.0f, false));

            Animations.GetWho().freezePause = poopAnimationTime;
            Animations.GetWho().canMove = false;
            Animations.GetWho().doEmote(12, false);
        }
        public static void AnimateMessingEnd(Body b)
        {

            if (b.IsFishing()) return;
            Game1.playSound("coin");
            Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Microsoft.Xna.Framework.Rectangle(192, 1152, Game1.tileSize, Game1.tileSize), 50f, 4, 0, Animations.GetWho().position.Value - new Vector2(Animations.GetWho().facingDirection.Value == 1 ? 0.0f : -Game1.tileSize, Game1.tileSize * 2), false, Animations.GetWho().facingDirection.Value == 1, Animations.GetWho().StandingPixel.Y / 10000f, 0.01f, Microsoft.Xna.Framework.Color.White, 1f, 0.0f, 0.0f, 0.0f, false));
        }

        public static void AnimateWetting(Body b, bool voluntary)
        {
            if (b.IsFishing() || !Animations.GetWho().canMove) return;

            Game1.playSound("wateringCan");

            if ((double)b.pants.wetness > (double)b.pants.absorbency)
            {
                WetTerrain();
            }

            if (voluntary)
                Animations.Say(Animations.GetData().Wet_Voluntary, b);
            else {
                bool notices = Regression.config.AlwaysNoticeAccidents || (double)b.bladderContinence + 0.2 > Regression.rnd.NextDouble();
                if (!notices) return;

                Animations.Say(Animations.GetData().Wet_Accident, b);
            }

            Animations.GetWho().jitterStrength = 0.5f;
            Animations.GetWho().freezePause = peeAnimationTime; //milliseconds
            Animations.GetWho().canMove = false;
            ((Character)Animations.GetWho()).doEmote(28, false);
        }

        // Pees in toilet or outside.
        public static void AnimatePee(Body b)
        {
            if (b.IsFishing() || !Animations.GetWho().canMove) return;

            Game1.playSound("wateringCan");

            if (b.InToilet())
                Animations.Say(Animations.GetData().Pee_Toilet, b);
            else
            {
                Animations.Say(Animations.GetData().Pee_Voluntary, b);
                WetTerrain();
            }

            Animations.GetWho().jitterStrength = 0.5f;
            Animations.GetWho().freezePause = peeAnimationTime; //milliseconds
            Animations.GetWho().canMove = false;
            ((Character)Animations.GetWho()).doEmote(28, false);
        }

        private static void WetTerrain()
        {
            var animatedSprite = new TemporaryAnimatedSprite(13, (Vector2)((Character)Game1.player).position.Value, Microsoft.Xna.Framework.Color.White, 10, ((Random)Game1.random).NextDouble() < 0.5, 70f, 0, (int)Game1.tileSize, 0.05f, -1, 0);
            ((GameLocation)Animations.GetWho().currentLocation).temporarySprites.Add(animatedSprite);

            HoeDirt terrainFeature;
            if (Animations.GetWho().currentLocation.terrainFeatures.ContainsKey(((Character)Animations.GetWho()).Tile) && (terrainFeature = Animations.GetWho().currentLocation.terrainFeatures[((Character)Animations.GetWho()).Tile] as HoeDirt) != null)
                terrainFeature.state.Value = 1;
        }

        public static void AnimateMorning(Body b)
        {
            bool flag = (double)b.pants.wetness > 0.0;
            bool second = (double)b.pants.messiness > 0.0;
            string msg = "" + Strings.RandString(Animations.GetData().Wake_Up_Underwear_State);
            if (second)
            {
                msg = msg + " " + Strings.ReplaceOptional(Strings.RandString(Animations.GetData().Messed_Bed), flag);
                if (!Regression.config.Easymode)
                    msg = msg + " " + Strings.ReplaceAndOr(Strings.RandString(Animations.GetData().Washing_Bedding), flag, second, "&");
            }
            else if (flag)
            {
                msg = msg + " " + Strings.RandString(Animations.GetData().Wet_Bed);
                if (!Regression.config.Easymode)
                    msg = msg + " " + Strings.ReplaceAndOr(Strings.RandString(Animations.GetData().Spot_Washing_Bedding), flag, second, "&");
            }
            Animations.Write(msg, b);
        }

        public static void AnimateNight(Body b)
        {
            bool first = b.numPottyPeeAtNight > 0;
            bool second = b.numPottyPooAtNight > 0;
            if (!(first | second) || !Regression.config.Wetting && !Regression.config.Messing)
              return;
            string toiletMsg = Strings.ReplaceAndOr(Strings.RandString(Animations.GetData().Toilet_Night), first, second, "&");

            if (b.numAccidentPooAtNight == 0 && b.numAccidentPeeAtNight == 0)
                toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", ".");
            else
            {
                if (!b.underwear.removable)
                {
                    toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", ", but couldn't get your $UNDERWEAR_NAME$ off!$HOW_MANY_TIMES");
                    toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", " So you still woke up$HOW_MANY_TIMES");
                } else
                {
                    toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", ", but you still woke up$HOW_MANY_TIMES");
                }
                if (b.numAccidentPeeAtNight > 0)
                    toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", " wet$HOW_MANY_TIMES");
                if (b.numAccidentPooAtNight > 0)
                    toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", " and messy$HOW_MANY_TIMES");
                if (b.numAccidentPooAtNight > 0 || b.numAccidentPeeAtNight > 0)
                    toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", "! Looks like someone really does need to be in their diapers at night$HOW_MANY_TIMES");
            }
            toiletMsg = Strings.InsertVariable(toiletMsg, "$HOW_MANY_TIMES", ".");
            Write(toiletMsg, b);
        }

        public static void AnimatePeeAttempt(Body b, Container container)
        {
            if (b.IsFishing()) return;
            if (!container.removable)
                Say(Animations.GetData().Wet_Attempt, b);
            else if (b.InToilet())
                Say(Animations.GetData().Pee_Toilet_Attempt, b);
            else
                Say(Animations.GetData().Pee_Attempt, b);
        }

        public static void AnimatePoopAttempt(Body b, bool inUnderwear)
        {

            if (b.IsFishing()) return;
            if (inUnderwear)
                Animations.Say(Animations.GetData().Mess_Attempt, b);
            else if (b.InToilet())
                Animations.Say(Animations.GetData().Poop_Toilet_Attempt, b);
            else
                Animations.Say(Animations.GetData().Poop_Attempt, b);
        }

        public static void AnimateWashingUnderwear(Container c)
        {
            if (c.MarkedForDestroy())
            {
                Animations.Write(Strings.InsertVariables(Animations.GetData().Overwashed_Underwear[0], (Body)null, c), (Body)null);
                Game1.player.reduceActiveItemByOne();
            }
            else
            {
                Animations.Write(Strings.InsertVariables(Strings.RandString(Animations.GetData().Washing_Underwear), (Body)null, c), (Body)null);
            }
        }

        public static void CheckPants(Body b)
        {
            StardewValley.Objects.Clothing pants = (StardewValley.Objects.Clothing)Animations.GetWho().pantsItem.Value;
            b.pants.name = pants.displayName;
            b.pants.description = pants.displayName;
            b.pants.plural = true;
            Animations.Say(Animations.GetData().LookPants[0] + " " + Strings.DescribeUnderwear(b.pants, null) + ".", b);
        }

        public static void CheckUnderwear(Body b)
        {
            Say(Animations.GetData().PeekWaistband[0] + " " + Strings.DescribeUnderwear(b.underwear, (string)null) + ".", b);
        }

        public static void DrawUnderwearIcon(Container c, int x, int y)
        {
            Microsoft.Xna.Framework.Color defaultColor = Microsoft.Xna.Framework.Color.White;

            Texture2D underwearSprites = Animations.GetSprites();
            Microsoft.Xna.Framework.Rectangle srcBoxCurrent = Animations.UnderwearRectangle(c, FullnessType.None, LARGE_SPRITE_DIM);

            Microsoft.Xna.Framework.Rectangle destBoxCurrent = new Microsoft.Xna.Framework.Rectangle(x, y, DIAPER_HUD_DIM, DIAPER_HUD_DIM);

            ((SpriteBatch)Game1.spriteBatch).Draw(underwearSprites, destBoxCurrent, srcBoxCurrent, defaultColor);
            if (Game1.getMouseX() >= x && Game1.getMouseX() <= x + DIAPER_HUD_DIM && Game1.getMouseY() >= y && Game1.getMouseY() <= y + DIAPER_HUD_DIM)
                {
                    string source = Strings.DescribeUnderwear(c, (string)null);
                    string str = source.First<char>().ToString().ToUpper() + source.Substring(1);
                    int num = Game1.tileSize * 6 + Game1.tileSize / 6;
                    IClickableMenu.drawHoverText((SpriteBatch)Game1.spriteBatch, Game1.parseText(str, (SpriteFont)Game1.tinyFont, num), (SpriteFont)Game1.smallFont, 0, 0, -1, (string)null, -1, (string[])null, (Item)null, 0, null, -1, -1, -1, 1f, (CraftingRecipe)null);
                }
        }

        private static void EndDrinking(Farmer who)
        {
            Animations.GetWho().completelyStopAnimatingOrDoingAction();
            Animations.GetWho().forceCanMove();
        }

        public static bool HandleVillagersInUnderwear(Body b, bool mess, bool overflow)
        {
            /*
             * - Leaking pee or poop, people around you will notice -> Medium area
             * - Pooping in underwear, people close to you will notice -> Adjacent area
             * - Peeing in underwear, no one will notice.
             */

            int radius = mess ? 2 : 0;
            if (overflow)
            {
                radius += 2;
            }

            int friendshipLoss = mess ? -2 : -1;

            //If we are messing, increase the radius of noticeability (stinky)
            //Double how much friendship we lose (mess is gross)
            if (mess)
            {
                radius *= 2;
                friendshipLoss *= 2;
            }

            if (radius == 0)
            {
                // Pee without overflow no one will notice
                return false;
            }

            NPC npc = getNearbyNPC(radius);
            if (npc == null) return false;
            List<string> npcType = getNPCType(npc);
            int heartLevelForNpc = Animations.GetWho().getFriendshipHeartLevelForNPC(npc.getName());

            //What did we do? Use to figure out the response.
            string responseKey = "soiled";

            //Animals only have a "nice" response
            if (npcType.Contains("animal"))
            {
                responseKey += "_nice";
                friendshipLoss = 0;
            }
            //If we have a really high relationship with the NPC, they're very nice about our accident
            else if (heartLevelForNpc >= 8)
            {
                responseKey += "_verynice";
                friendshipLoss = 0;
            }
            else
                //Otherwise they'll be mean or nice depending on how much friendship we're losing
                responseKey = friendshipLoss < 0 ? responseKey + "_mean" : responseKey + "_nice";

            return NotifyVillager(b, npc, mess, friendshipLoss, responseKey);
        }

        public static bool HandleVillagersInPublic(Body b, bool mess)
        {
            /*
             * - Peeing or pooping outside, everyone on the screen can see it -> Wide area
             */

            int radius = 12;
            int friendshipLoss = -2;

            //If we are messing, increase the radius of noticeability (stinky)
            //Double how much friendship we lose (mess is gross)
            if (mess) {
                radius *= 2;
                friendshipLoss *= 2;
            }

            return NotifyVillager(b, getNearbyNPC(radius), mess, friendshipLoss, "ground");
        }

        private static NPC getNearbyNPC(int radius)
        {
            if (Utility.isThereAFarmerOrCharacterWithinDistance(((Character)Animations.GetWho()).Tile, radius, (GameLocation)Game1.currentLocation) is not NPC npc || NPC_LIST.Contains(npc.Name))
                return null;
            return npc;
        }

        private static List<string> getNPCType(NPC npc)
        {
            //Make a list based on who saw us.
            List<string> npcType = new List<string>();
            if (npc is Horse || npc is Pet)
            {
                npcType.Add("animal");
            }
            else
            {
                switch (npc.Age)
                {
                    case 0:
                        npcType.Add("adult");
                        break;
                    case 1:
                        npcType.Add("teen");
                        break;
                    case 2:
                        npcType.Add("kid");
                        break;
                }
                npcType.Add(npc.getName().ToLower());
            }
            return npcType;
        }

        private static bool NotifyVillager(Body b, NPC npc, bool mess, int friendshipLoss, string responseKey)
        {
            if (npc is null) return false;

            //Reduce the loss if the person likes you (more forgiving)
            int heartLevelForNpc = Animations.GetWho().getFriendshipHeartLevelForNPC(npc.getName());

            //Does this leave the possibility of friendship gain if we have enough hearts already? Maybe because they find the vulnerability endearing?
            int actualLoss = friendshipLoss + (heartLevelForNpc - 2) * 10;

            //If we didn't lose any friendship, or we disabled friendship penalties, then don't adjust the value
            if (actualLoss < 0 && !Regression.config.NoFriendshipPenalty)
                Animations.GetWho().changeFriendship(actualLoss, npc);

            //Make a list based on who saw us.
            List<string> npcType = getNPCType(npc);
            string npcName = "";
            if (npc is Horse || npc is Pet)
            {
                npcName += string.Format("{0}: ", npc.Name);
            }

            List<string> stringList3 = new List<string>();
            foreach (string key2 in npcType)
            {
                Dictionary<string, string[]> dictionary;
                string[] strArray;
                if (Animations.GetData().Villager_Reactions.TryGetValue(key2, out dictionary) && dictionary.TryGetValue(responseKey, out strArray))
                    stringList3.AddRange((IEnumerable<string>)strArray);
            }

            //Construct and say Statement
            string npcStatement = npcName + Strings.InsertVariables(Strings.ReplaceAndOr(Strings.RandString(stringList3.ToArray()), !mess, mess, "&"), b, (Container)null);
            npc.setNewDialogue(new Dialogue(npc, null, npcStatement), true, true);
            Game1.drawDialogue(npc);
            return false;
        }

        public static Texture2D LoadTexture(string file)
        {
            return Regression.help.ModContent.Load<Texture2D>(Path.Combine("Assets", file));
        }

        public static void Say(string msg, Body b = null)
        {
            Game1.showGlobalMessage(Strings.InsertVariables(msg, b, (Container)null));
        }

        public static void Say(string[] msgs, Body b = null)
        {
            Animations.Say(Strings.RandString(msgs), b);
        }

        public static Microsoft.Xna.Framework.Rectangle UnderwearRectangle(Container c, FullnessType type = FullnessType.None, int height = LARGE_SPRITE_DIM)
        {
            if (c.spriteIndex == -1)
                throw new Exception("Invalid sprite index.");
            int num = 0;
            //Using switch statement instead of ternary operator for better readability and to add another type.
            if (type != FullnessType.None)
            {
                switch (type)
                {
                    case (FullnessType.Clear):
                        {
                            num = 0;
                            break;
                        }
                    case (FullnessType.Messy):
                        {
                            num = LARGE_SPRITE_DIM;
                            break;
                        }
                    case (FullnessType.Wet):
                        {
                            num = LARGE_SPRITE_DIM * 2;
                            break;
                        }
                    case (FullnessType.WetMessy):
                        {
                            num = LARGE_SPRITE_DIM * 3;
                            break;
                        }
                    case (FullnessType.Drying):
                        {
                            num = LARGE_SPRITE_DIM * 4;
                            break;
                        }
                    default:
                        {
                            num = 0;
                            break;
                        }
                }
            }
            else
            {
                if (!c.IsDrying())
                {
                    if ((double)c.messiness <= .0f)
                    {
                        if ((double)c.wetness <= .0f)
                            num = 0;
                        else
                            num = LARGE_SPRITE_DIM;
                    }
                    else
                    {
                        if ((double)c.wetness <= .0f)
                            num = LARGE_SPRITE_DIM * 2;
                        else
                            num = LARGE_SPRITE_DIM * 3;
                    }
                }
                else
                {
                    num = LARGE_SPRITE_DIM * 4;
                }
            }
            return new Microsoft.Xna.Framework.Rectangle(c.spriteIndex * LARGE_SPRITE_DIM, num + (LARGE_SPRITE_DIM - height), LARGE_SPRITE_DIM, height);
        }

        public static void Warn(string msg, Body b = null)
        {
            Game1.addHUDMessage(new HUDMessage(Strings.InsertVariables(msg, b, (Container)null), 2));
        }

        public static void Warn(string[] msgs, Body b = null)
        {
            Animations.Warn(Strings.RandString(msgs), b);
        }

        public static void Write(string msg, Body b = null, int delay = 0)
        {
            DelayedAction.showDialogueAfterDelay(Strings.InsertVariables(msg, b, (Container)null), delay);
        }

        public static void Write(string[] msgs, Body b = null, int delay = 0)
        {
            Animations.Write(Strings.RandString(msgs), b, delay);
        }
    }
}
