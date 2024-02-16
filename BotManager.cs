using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CoolFishNS.Bots;
using CoolFishNS.Bots.CoolFishBot;
using CoolFishNS.Bots.PoolFishingBot;
using CoolFishNS.Management.CoolManager;
using CoolFishNS.Management.CoolManager.HookingLua;
using CoolFishNS.PluginSystem;
using CoolFishNS.Properties;
using CoolFishNS.Utilities;
using GreyMagic;

namespace CoolFishNS.Management
{
    /// <summary>
    ///     Bot manager class for performing operations on IBot implemented classes
    /// </summary>
    public static class BotManager
    {
        internal static readonly Dictionary<string, IBot> LoadedBots = new Dictionary<string, IBot>();

        internal static bool WasCut;

        static BotManager()
        {
            LoadBot(new CoolFishBot(), true);
            LoadBot(new PoolFishingBot());
        }

        public static ExternalProcessReader Memory { get; private set; }

        /// <summary>
        ///     Currently active IBot object that the user interface will interact with.
        ///     This field should be set to whatever Bot object you want to respond to UI functions (Start, Stop, etc.)
        /// </summary>
        public static IBot ActiveBot { get; private set; }


        private static bool IsReadyToBot
        {
            get { return Memory != null && ActiveBot != null && Memory.IsProcessOpen && DxHook.Instance.IsApplied && !ActiveBot.IsRunning(); }
        }

        /// <summary>
        ///     Loads an IBot implementing class into CoolFish's BotManager for display and use from the interface
        /// </summary>
        /// <param name="botToLoad">IBot implementing instance to load into CoolFish</param>
        /// <param name="setAsActive">true to set the loaded bot as the currently active one; otherwise, set to false</param>
        public static void LoadBot(IBot botToLoad, bool setAsActive = false)
        {
            if (!IsBotLoaded(botToLoad))
            {
                LoadedBots.Add(GetBotId(botToLoad), botToLoad);
            }
            else
            {
                Logging.Write("Bot \"" + GetBotId(botToLoad) + "\" has already been loaded. Skipping load...");
            }

            if (setAsActive)
            {
                SetActiveBot(botToLoad);
            }
        }

        /// <summary>
        ///     Returns whether or not a bot with a particular unique keyId has been loaded or not
        /// </summary>
        /// <param name="bot">IBot to look up</param>
        /// <returns>true if the bot is already loaded; otherwise, false</returns>
        public static bool IsBotLoaded(IBot bot)
        {
            return LoadedBots.ContainsKey(GetBotId(bot));
        }

        /// <summary>
        ///     Get the unique identifier string the BotManager uses to compare IBot classes.
        ///     Currently implemented as "bot.GetName() - bot.GetVersion()"
        ///     No two bots should be loaded with the same name and version combination
        /// </summary>
        /// <param name="bot">bot to get the Id of</param>
        /// <returns>string identifier of the IBot class</returns>
        public static string GetBotId(IBot bot)
        {
            return bot.GetName() + "-" + bot.GetVersion();
        }

        /// <summary>
        ///     Sets the actively running bot based on the passed unique keyId
        /// </summary>
        /// <param name="bot">IBot to set as active</param>
        /// <returns>true if bot was set as active; false, if bot is not loaded and was not set as active</returns>
        public static bool SetActiveBot(IBot bot)
        {
            if (!IsBotLoaded(bot))
            {
                Logging.Write("Bot \"" + GetBotId(bot) + "\" has not yet been loaded");
                return false;
            }

            StopActiveBot();
            ActiveBot = LoadedBots[GetBotId(bot)];
            return true;
        }

        /// <summary>
        ///     Attach all manipulation related classes to the passed process.
        ///     ObjectManager and Hook related operations will be available after this call
        /// </summary>
        /// <param name="process"></param>
        public static void AttachToProcess(Process process)
        {
            if (WasCut)
            {
                return;
            }
            StopActiveBot();
            try
            {
                if (Offsets.FindOffsets(process))
                {
                    Memory = new ExternalProcessReader(process);


                    if (DxHook.Instance.Apply())
                    {
                        Logging.Write(LocalSettings.Translations["Attached to"] + ": " +
                                      process.Id);
                    }
                    else
                    {
                        Logging.Write(LocalSettings.Translations["Error"]);

                        Memory.Dispose();
                        Memory = null;
                    }
                }
                else
                {
                    Logging.Write(LocalSettings.Translations["Unhandled Exception"]);
                }
            }
            catch (Exception ex)
            {

                Logging.Write(LocalSettings.Translations["Unhandled Exception"]);
                Logging.Log(ex);
            }
            
        }

        /// <summary>
        ///     Start the currently Active Bot
        /// </summary>
        public static void StartActiveBot()
        {
            if (WasCut)
            {
                return;
            }
            if (IsReadyToBot)
            {
                ActiveBot.StartBot();
            }
            else
            {
                Logging.Write(Resources.BotIsNotReadyError);
            }
        }

        /// <summary>
        ///     Stop the currently Active Bot
        /// </summary>
        public static void StopActiveBot()
        {
            if (ActiveBot != null && ActiveBot.IsRunning())
            {
                ActiveBot.StopBot();
            }
        }

        internal static void StartUp()
        {
            LocalSettings.LoadSettings();

            PluginManager.LoadPlugins();

            PluginManager.StartPlugins();

            Logging.Log("Start Up.");
        }

        internal static void ShutDown()
        {
            StopActiveBot();

            PluginManager.StopPlugins();

            PluginManager.ShutDownPlugins();

            LocalSettings.SaveSettings();

            DxHook.Instance.Dispose();

            if (Memory != null)
            {
                Memory.Dispose();
            }
            

            Logging.Log("Shut Down.");
        }

        /// <summary>
        ///     Get the currently logged in toon's name
        /// </summary>
        /// <returns>string of the player's name</returns>
        public static string GetToonName()
        {
            return Offsets.Addresses.ContainsKey("PlayerName") ? Memory.ReadString(Offsets.Addresses["PlayerName"], Encoding.UTF8) : string.Empty;
        }
    }
}