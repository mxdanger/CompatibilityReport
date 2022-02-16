﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    /// <summary>OneTimeAction gathers information from the Steam Workshop pages for specific items and logs those to a file.</summary>
    /// <remarks>This process can take a long time, depending on the number of items; roughly half a second per item.</remarks>
    public static class OneTimeAction
    {
        // These constants define which info is being gathered.
        private const string title = "Roads that require the Adaptive Networks mod.\n=============================================\n" +
            "([MAYBE] means the description mentions the mod, but the mod is not set as required)\n\n";
        private const string steamUrl = "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Road";
        private const int maxPages = 200;
        private const ulong requiredItem1 = 2414618415;     // Adaptive Networks stable
        private const ulong requiredItem2 = 2669938594;     // Adaptive Networks alpha
        private const string descriptionString1 = "Adaptive Networks";
        private const string descriptionString2 = "Adaptive Roads";


        /// <summary>Starts the OneTimeAction. Downloads Steam webpages for all items and logs info to a file.</summary>
        public static void Start()
        {
            List<ulong> steamIDs = new List<ulong>();

            if (GetBasicInfo(steamIDs))
            {
                GetDetails(steamIDs);

                // Todo 0.8 This can be removed after updater settings are implemented. Make disable-after-run an option in the settings file.
                Toolkit.MoveFile(Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_OneTimeAction.enabled"),
                    Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_OneTimeAction.disabled"));
            }
        }


        private static bool GetBasicInfo(List<ulong> steamIDs)
        {
            Logger.UpdaterLog("Updater started the one-time action. This may take quite some time.");

            int pageNumber = 0;

            Logger.UpdaterLog($"Downloading item listings from { steamUrl }");

            // Download and read pages until we find no more items, or we reach a maximum number of pages, to avoid missing the mark and continuing for eternity.
            while (pageNumber < maxPages)
            {
                pageNumber++;
                string url = $"{ steamUrl }&p={ pageNumber }";

                if (!Toolkit.Download(url, ModSettings.TempDownloadFullPath))
                {
                    pageNumber--;

                    Logger.UpdaterLog($"Download process interrupted due to a permanent error while downloading { url }", Logger.Error);
                    break;
                }

                if (!ReadItemListingPage(steamIDs))
                {
                    pageNumber--;
                    break;
                }
            }

            steamIDs.Sort();

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            Logger.UpdaterLog($"Updater finished downloading { pageNumber } Steam Workshop 'item listing' pages and found { steamIDs.Count } items.");

            return steamIDs.Count > 0;
        }


        /// <summary>Extracts Steam IDs for all items from a downloaded item listing page and adds them to a list.</summary>
        /// <returns>True if items were found, false otherwise.</returns>
        private static bool ReadItemListingPage(List<ulong> steamIDs)
        {
            bool itemsFound = false;
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    // Search for the identifying string for the next item; continue with next line if not found.
                    if (!line.Contains(ModSettings.SearchModStart))
                    {
                        continue;
                    }

                    ulong steamID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchSteamIDLeft, ModSettings.SearchSteamIDRight));

                    if (steamID == 0)
                    {
                        Logger.UpdaterLog($"Steam ID not recognized on HTML line: { line }", Logger.Error);
                        continue;
                    }

                    steamIDs.Add(steamID);
                    itemsFound = true;
                }
            }

            return itemsFound;
        }


        /// <summary>Downloads individual item pages from the Steam Workshop to get detailed item information for all items in the list.</summary>
        private static void GetDetails(List<ulong> steamIDs)
        {
            Stopwatch timer = Stopwatch.StartNew();
            int numberOfItems = steamIDs.Count;
            long estimatedMilliseconds = ModSettings.EstimatedMillisecondsPerModPage * numberOfItems;

            Logger.UpdaterLog($"Updater started downloading { numberOfItems } individual Steam Workshop item pages. This should take about " +
                $"{ Toolkit.TimeString(estimatedMilliseconds) } and should be ready around { DateTime.Now.AddMilliseconds(estimatedMilliseconds + 30 * 1000):HH:mm}.");

            if (!Toolkit.SaveToFile($"{ title }", Path.Combine(ModSettings.UpdaterPath, ModSettings.OneTimeActionFileName)))
            {
                Logger.UpdaterLog($"Could not write to file \"{ Path.Combine(ModSettings.UpdaterPath, ModSettings.OneTimeActionFileName) }\".", Logger.Error);
                return;
            }

            int itemsDownloaded = 0;
            int failedDownloads = 0;

            foreach (ulong steamID in steamIDs)
            {
                if (!Toolkit.Download(Toolkit.GetWorkshopUrl(steamID), ModSettings.TempDownloadFullPath))
                {
                    failedDownloads++;

                    Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for { steamID }.", Logger.Error);

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        continue;
                    }
                    else
                    {
                        Logger.UpdaterLog("Download process stopped prematurely.", Logger.Error);
                        break;
                    }
                }

                itemsDownloaded++;

                if (itemsDownloaded % 100 == 0)
                {
                    Logger.UpdaterLog($"{ itemsDownloaded }/{ numberOfItems } item pages downloaded.");
                }

                if (!ReadItemPage(steamID))
                {
                    Logger.UpdaterLog($"Item info not found for { steamID }.", Logger.Error);
                }
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            Logger.UpdaterLog($"Updater finished downloading { itemsDownloaded } individual Steam Workshop item pages in " +
                $"{ Toolkit.TimeString(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.");
        }


        /// <summary>Extracts detailed item information from a downloaded item page and logs the item if it matches the criteria.</summary>
        /// <returns>True if succesful, false if there was an error with the item page.</returns>
        public static bool ReadItemPage(ulong steamID)
        {
            bool steamIDmatched = false;
            string itemName = "";
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (!steamIDmatched)
                    {
                        steamIDmatched = line.Contains($"{ ModSettings.SearchSteamID }{ steamID }");

                        if (line.Contains(ModSettings.SearchItemNotFound))
                        {
                            Logger.UpdaterLog($"Can't read the Steam Workshop page for { steamID }.", Logger.Warning);
                            return false;
                        }

                        // Keep reading lines until we find the Steam ID.
                        continue;
                    }

                    // Item name.
                    if (line.Contains(ModSettings.SearchModNameLeft))
                    {
                        itemName = Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchModNameLeft, ModSettings.SearchModNameRight));

                        if (string.IsNullOrEmpty(itemName))
                        {
                            // An empty item name might be an error, although there is at least one Steam Workshop item without a name (ofcourse there is).
                            Logger.UpdaterLog($"Item name not found for { steamID }. This could be an actual unnamed item, or a Steam error.", Logger.Warning);
                        }
                    }

                    // Required mods and assets. The search string is a container with all required items on the next lines.
                    else if (line.Contains(ModSettings.SearchRequiredMod))
                    {
                        // Get all required items from the next lines, until we find no more. Max. 50 times to avoid an infinite loop.
                        for (var i = 1; i <= 50; i++)
                        {
                            line = reader.ReadLine();
                            ulong requiredID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchRequiredModLeft, ModSettings.SearchRequiredModRight));

                            if (requiredID == 0)
                            {
                                break;
                            }
                            
                            if (requiredID == requiredItem1 || requiredID == requiredItem2)
                            {
                                Toolkit.SaveToFile($"{ itemName }, { Toolkit.GetWorkshopUrl(steamID) }\n", 
                                    Path.Combine(ModSettings.UpdaterPath, ModSettings.OneTimeActionFileName), append: true);
                                return true;
                            }

                            line = reader.ReadLine();
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                        }
                    }

                    // Description.
                    else if (line.Contains(ModSettings.SearchDescription))
                    {
                        // Usually, the complete description is on one line. However, it might be split when using certain bbcode. Combine up to 30 lines.
                        for (var i = 1; i <= 30; i++)
                        {
                            line = reader.ReadLine();

                            // Break out of the for loop when a line is found that marks the end of the description.
                            if (line == ModSettings.SearchDescriptionNextLine || line == ModSettings.SearchDescriptionNextSection)
                            {
                                break;
                            }

                            if (line.Contains(descriptionString1) || line.Contains(descriptionString2))
                            {
                                Toolkit.SaveToFile($"[MAYBE] { itemName }, { Toolkit.GetWorkshopUrl(steamID) }\n", 
                                    Path.Combine(ModSettings.UpdaterPath, ModSettings.OneTimeActionFileName), append: true);
                                return true;
                            }
                        }

                        // Description is the last info we need from the page, so break out of the while loop.
                        break;
                    }
                }
            }

            if (!steamIDmatched)
            {
                // We didn't find a Steam ID on the page, but no error page either. Must be a download issue or another Steam error.
                Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { steamID }.", Logger.Warning);
                return false;
            }

            return true;
        }
    }
}
