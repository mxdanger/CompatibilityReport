﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CompatibilityReport.Util;


namespace CompatibilityReport.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable][XmlRoot(ModSettings.xmlRoot)] public class Catalog
    {
        // Catalog structure version (major version); will only change on structural changes in the xml that make it incompatible with a previous structure version
        public uint StructureVersion { get; private set; }

        // Catalog version and date; version always increases and never resets, even when going to a new StructureVersion
        public uint Version { get; private set; }

        public DateTime UpdateDate { get; private set; }

        // Game version this catalog was created for; 'Version' is not serializable, so a converted string is used
        [XmlIgnore] internal Version CompatibleGameVersion { get; private set; }
        public string CompatibleGameVersionString { get; private set; }

        // A note about the catalog, displayed in the report header
        public string Note { get; private set; }

        // Intro and footer for the text report
        public string ReportIntroText { get; private set; }

        public string ReportFooterText { get; private set; }

        // The actual mod data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();

        public List<Group> Groups { get; private set; } = new List<Group>();

        public List<Author> Authors { get; private set; } = new List<Author>();

        // Assets that show up as required items; listed to see the difference between a required asset and a required mod we don't know (unlisted or removed mod)
        [XmlArrayItem("SteamID")] public List<ulong> RequiredAssets { get; private set; } = new List<ulong>();


        // Dictionaries to make searching easier and faster
        [XmlIgnore] internal Dictionary<ulong, Mod> ModDictionary { get; private set; } = new Dictionary<ulong, Mod>();
        
        [XmlIgnore] internal Dictionary<ulong, Group> GroupDictionary { get; private set; } = new Dictionary<ulong, Group>();

        [XmlIgnore] internal Dictionary<ulong, Author> AuthorIDDictionary { get; private set; } = new Dictionary<ulong, Author>();

        [XmlIgnore] internal Dictionary<string, Author> AuthorURLDictionary { get; private set; } = new Dictionary<string, Author>();


        // The total number of mods in the catalog
        [XmlIgnore] internal int Count { get; private set; }
        [XmlIgnore] internal int ReviewCount { get; private set; }


        // Default constructor, used when creating an empty catalog for reading from disk
        public Catalog()
        {
            // Nothing to do here
        }


        // Constructor with 3 to 5 parameters, used when creating a new catalog
        internal Catalog(uint version,
                         DateTime updateDate,
                         string note,
                         string reportIntroText = null,
                         string reportFooterText = null)
        {
            StructureVersion = ModSettings.currentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = GameVersion.Current;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            Note = note ?? "";

            ReportIntroText = reportIntroText ?? ModSettings.defaultIntroText;

            ReportFooterText = reportFooterText ?? ModSettings.defaultFooterText;
        }


        // Constructor with all parameters, used when converting an old catalog
        internal Catalog(uint version, DateTime updateDate, Version compatibleGameVersion, string note, string reportIntroText, string reportFooterText, 
            List<Mod> mods, List<Compatibility> modCompatibilities, List<Group> groups, List<Author> modAuthors, List<ulong> requiredAssets)
        {
            StructureVersion = ModSettings.currentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = compatibleGameVersion;
            
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            Note = note ?? "";

            ReportIntroText = reportIntroText ?? "";

            ReportFooterText = reportFooterText ?? "";

            Mods = mods ?? new List<Mod>();

            Compatibilities = modCompatibilities ?? new List<Compatibility>();

            Groups = groups ?? new List<Group>();

            Authors = modAuthors ?? new List<Author>();

            RequiredAssets = requiredAssets ?? new List<ulong>();
        }


        // Update the catalog; all fields are optional, only supplied fields are updated; the lists can be updated directly, version has it's own update method
        internal void Update(string note = null, 
                             Version compatibleGameVersion = null, 
                             string reportIntroText = null, 
                             string reportFooterText = null)
        {
            Note = note ?? Note;

            CompatibleGameVersion = compatibleGameVersion ?? CompatibleGameVersion;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            ReportIntroText = reportIntroText ?? ReportIntroText;

            ReportFooterText = reportFooterText ?? ReportFooterText;
        }


        // Catalog version string, for reporting and logging
        internal string VersionString()
        {
            return $"{ StructureVersion }.{ Version:D4}";
        }


        // Increase the version with a new update date (defaults to now); used for the Updater
        internal void NewVersion(DateTime? updated = null)
        {
            Version++;

            UpdateDate = updated ?? DateTime.Now;
        }


        // Change the compatible game version to a higher version
        internal bool UpdateGameVersion(Version newGameVersion)
        {
            // Exit on incorrect game version, or if the new game version is not higher than the current
            if (newGameVersion == null || newGameVersion <= CompatibleGameVersion)
            {
                return false;
            }

            CompatibleGameVersion = newGameVersion;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            return true;
        }


        // Check if the ID is a valid existing or non-existing ID
        internal bool IsValidID(ulong steamID,
                                bool allowBuiltin = true,
                                bool allowGroup = false,
                                bool shouldExist = true)
        {
            // Check if the ID is a valid mod or group ID
            bool valid = steamID > ModSettings.highestFakeID ||
                         (allowBuiltin && ModSettings.BuiltinMods.ContainsValue(steamID)) ||
                         (allowGroup && steamID >= ModSettings.lowestGroupID && steamID <= ModSettings.highestGroupID);

            // Check if the mod or group already exists
            bool exists = ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID) || ActiveCatalog.Instance.GroupDictionary.ContainsKey(steamID);

            return valid && (shouldExist ? exists : !exists);
        }


        // Add or update a catalog mod. Exclusions for required mods have a separate method
        internal Mod AddOrUpdateMod(ulong steamID,
                                    string name = null,
                                    ulong authorID = 0,
                                    string authorURL = "",
                                    DateTime? published = null,
                                    DateTime? updated = null,
                                    string archiveURL = null,
                                    string sourceURL = null,
                                    string compatibleGameVersionString = null,
                                    List<Enums.DLC> requiredDLC = null,
                                    List<ulong> requiredMods = null,
                                    List<ulong> successors = null,
                                    List<ulong> alternatives = null,
                                    List<ulong> recommendations = null,
                                    Enums.ModStability stability = Enums.ModStability.Undefined,
                                    string stabilityNote = null,
                                    List<Enums.ModStatus> statuses = null,
                                    string genericNote = null,
                                    bool? exclusionForSourceURL = null, 
                                    bool? exclusionForGameVersion = null, 
                                    bool? exclusionForNoDescription = null,
                                    List<Enums.DLC> exclusionForRequiredDLC = null,
                                    DateTime? reviewUpdated = null,
                                    DateTime? autoReviewUpdated = null,
                                    string extraChangeNote = null)
        {
            // Create a new mod
            Mod mod;

            if (!ModDictionary.ContainsKey(steamID))
            {
                // Mod doesn't exist yet
                mod = new Mod(steamID, name, authorID, authorURL);

                // Add the mod to the list and dictionary
                Mods.Add(mod);

                ModDictionary.Add(steamID, mod);
            }
            else
            {
                // This mod already exists; update the existing one
                mod = ModDictionary[steamID];
            }

            // Add all info to the mod
            mod.Update(name, published, updated, authorID, authorURL, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC, requiredMods, successors, 
                alternatives, recommendations, stability, stabilityNote, statuses, genericNote, exclusionForSourceURL, exclusionForGameVersion, exclusionForNoDescription, 
                exclusionForRequiredDLC, exclusionForRequiredMods: null, reviewUpdated, autoReviewUpdated, extraChangeNote: extraChangeNote);

            // Return a reference to the new mod
            return mod;
        }


        // Get the author, or return null when the author doesn't exist
        internal Author GetAuthor(ulong authorID, string authorURL)
        {
            if (authorID != 0 && AuthorIDDictionary.ContainsKey(authorID))
            {
                return AuthorIDDictionary[authorID];
            }
            else if (!string.IsNullOrEmpty(authorURL) && AuthorURLDictionary.ContainsKey(authorURL))
            {
                return AuthorURLDictionary[authorURL];
            }
            else
            {
                return null;
            }
        }


        // Add a new author to the catalog; return the author or null if they couldn't be added
        internal Author AddAuthor(ulong authorID,
                                  string authorURL,
                                  string name,
                                  DateTime lastSeen = default,
                                  bool retired = false,
                                  string changeNoteString = null)
        {
            if (AuthorIDDictionary.ContainsKey(authorID) || AuthorURLDictionary.ContainsKey(authorURL ?? "")) 
            {
                // Author already exists
                Logger.Log($"Tried to add an author with a profile ID ({ authorID }) or custom URL ({ authorURL }) that is already used. Author NOT added.", Logger.error);

                return null;
            }

            // Create change notes list
            List<string> changeNotes = new List<string>();

            if (!string.IsNullOrEmpty(changeNoteString))
            {
                changeNotes.Add(changeNoteString);
            }
            
            // Create a new author
            Author author = new Author(authorID, authorURL, name, lastSeen, retired, changeNotes);

            // Add the author to the list
            Authors.Add(author);

            // Add the new author to one or two dictionaries
            if (authorID != 0)
            {
                AuthorIDDictionary.Add(authorID, author);
            }

            if (!string.IsNullOrEmpty(authorURL))
            {
                AuthorURLDictionary.Add(authorURL, author);
            }

            // Return a reference to the new author
            return author;
        }


        // Get the group this mod is a member of, if any
        internal Group GetGroup(ulong steamID)
        {
            return steamID == 0 ? default : Groups.FirstOrDefault(x => x.GroupMembers.Contains(steamID));
        }


        // Check if a mod is a group member
        internal bool IsGroupMember(ulong steamID)
        {
            return GetGroup(steamID) != default;
        }


        // Add a new group to the catalog; return the group, or null if the group couldn't be added; NOTE: a mod can only be in one group
        internal Group AddGroup(string name, List<ulong> groupMembers)
        {
            // Check if none of the steam IDs is already in a group, or is a group itself
            foreach (ulong groupMember in groupMembers ?? new List<ulong>())
            {
                if (groupMember >= ModSettings.lowestGroupID && groupMember <= ModSettings.highestGroupID)
                {
                    Logger.Log($"Could not add a new group ({ name }) because { groupMember } is a group and groups can't be nested.", Logger.error);

                    return null;
                }

                if (IsGroupMember(groupMember))
                {
                    Logger.Log($"Could not add a new group ({ name }) because [Steam ID { groupMember }] is already member of another group.", Logger.error);

                    return null;
                }
            }

            // Get a new group ID
            ulong newGroupID;

            if (GroupDictionary?.Any() != true)
            {
                // No groups yet, so use the lowest group ID
                newGroupID = ModSettings.lowestGroupID;
            }
            else
            {
                // Set it one higher than the highest used group ID
                newGroupID = GroupDictionary.Keys.Max() + 1;

                if (newGroupID > ModSettings.highestGroupID)
                {
                    Logger.Log($"Could not add a new group ({ name }) because we ran out of group IDs.", Logger.error);

                    return null;
                }
            }

            // Add the new group to the list and dictionary
            Group newGroup = new Group(newGroupID, name, groupMembers);

            Groups.Add(newGroup);

            GroupDictionary.Add(newGroup.GroupID, newGroup);

            // Replace group members with the group, as required mods anywhere in the catalog
            foreach (ulong groupMemberID in newGroup.GroupMembers)
            {
                ReplaceRequiredModByGroup(groupMemberID);
            }

            // Return a reference to the new group
            return newGroup;
        }


        // Add a new member to a group; NOTE: a mod can only be in one group
        internal bool AddGroupMember(ulong groupID, ulong newGroupMember)
        {
            // Exit if the group or mod don't exist, or if the mod is already in a group
            if (!GroupDictionary.ContainsKey(groupID) || !ModDictionary.ContainsKey(newGroupMember) || IsGroupMember(newGroupMember))
            {
                return false;
            }

            // Add the mod to the group
            GroupDictionary[groupID].GroupMembers.Add(newGroupMember);

            // Replace the mod as required mod by the group, everywhere in the catalog; this will also duplicate an exclusion (if any) to the group and other group members
            ReplaceRequiredModByGroup(newGroupMember);

            return true;
        }


        // Replace a required mod by the group it is a member of. Returns true if the mod was replaced.
        internal bool ReplaceRequiredModByGroup(ulong requiredModID)
        {
            Group requiredGroup = GetGroup(requiredModID);

            // Exit if this mod is not in a group
            if (requiredGroup == default)
            {
                return false;
            }

            // Get all mods that have this required mod
            List<Mod> modList = Mods.FindAll(x => x.RequiredMods.Contains(requiredModID));

            foreach (Mod mod in modList)
            {
                // Remove the mod ID and add the group ID
                mod.RequiredMods.Remove(requiredModID);

                if (!mod.RequiredMods.Contains(requiredGroup.GroupID))
                {
                    mod.RequiredMods.Add(requiredGroup.GroupID);
                }

                // Log to the most likely log
                if (ModSettings.UpdaterEnabled)
                {
                    Logger.UpdaterLog($"Replaced required mod { ModDictionary[requiredModID].ToString() } with { requiredGroup.ToString() }, " +
                        $"for { mod.ToString(cutOff: false) }.");
                }
                else
                {
                    Logger.Log($"Replaced required mod { ModDictionary[requiredModID].ToString() } with { requiredGroup.ToString() }, " +
                        $"for { mod.ToString(cutOff: false) }.");
                }

                // Add exclusion for the group if it exists for the old required mod or already exists for the group. This will create exclusions for all group members.
                if (mod.ExclusionForRequiredMods.Contains(requiredModID) || mod.ExclusionForRequiredMods.Contains(requiredGroup.GroupID))
                {
                    AddExclusionForRequiredMods(mod, requiredGroup.GroupID);
                }
            }

            return true;
        }


        // Add a new compatibility to the catalog; return the compatibility, or null if the compatibility couldn't be added
        internal Compatibility AddCompatibility(ulong steamID1,
                                                ulong steamID2,
                                                List<Enums.CompatibilityStatus> statuses,
                                                string note = "")
        {
            if ((steamID1 >= ModSettings.lowestGroupID && steamID1 <= ModSettings.highestGroupID) 
                || (steamID2 >= ModSettings.lowestGroupID && steamID2 <= ModSettings.highestGroupID))
            {
                Logger.Log($"Tried to add a compatibility with a group instead of a mod, which is not supported. Compatibility NOT added.", Logger.error);

                return null;
            }

            if (Compatibilities.Find(x => x.SteamID1 == steamID1 && x.SteamID2 == steamID2) != default)
            {
                // A compatibility already exists between these Steam IDs
                Logger.Log($"Tried to add a compatibility while one already exists between [Steam ID { steamID1 }] and [Steam ID { steamID2 }]. " + 
                    "Compatibility NOT added.", Logger.error);

                return null;
            }

            // Check if a mirrored compatibility already exists; this is allowed for some statuses, but not all  [Todo 0.4] Can we allow all compatibilities mirrored?
            if (Compatibilities.Find(x => x.SteamID1 == steamID2 && x.SteamID2 == steamID1 && 
                !x.Statuses.Contains(Enums.CompatibilityStatus.NewerVersion) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToAuthor) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToUsers) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.CompatibleAccordingToAuthor)) != null)
            {
                Logger.Log($"Tried to add a compatibility between [Steam ID { steamID1 }] and [Steam ID { steamID2 }] while a reversed one already exists . " +
                    "Compatibility NOT added.", Logger.error);

                return null;
            }

            // Add a new compatibility to the list
            Compatibility compatibility = new Compatibility(steamID1, steamID2, statuses, note);

            Compatibilities.Add(compatibility);
            
            // Return a reference to the new compatibility
            return compatibility;
        }


        // Add an exclusion for a required mod, including for group and group members
        internal void AddExclusionForRequiredMods(Mod mod, ulong requiredID)
        {
            // Add exclusion
            mod.AddExclusionForRequiredMods(requiredID);

            if (IsGroupMember(requiredID))
            {
                // SubItem is a group member. Add exclusion for the group and all members by calling AddExclusion with the group ID. Should not cause an infinite loop.
                AddExclusionForRequiredMods(mod, GetGroup(requiredID).GroupID);
            }
            else if (GroupDictionary.ContainsKey(requiredID))
            {
                // SubItem is a group. Add exclusion for all group members. This is done manually here and not by calling AddExclusion again, to avoid an infinite loop.
                foreach (ulong groupMember in GroupDictionary[requiredID].GroupMembers)
                {
                    mod.AddExclusionForRequiredMods(groupMember);
                }
            }
        }


        // Remove an exclusion from the catalog
        internal bool RemoveExclusionForRequiredMods(Mod mod, ulong requiredID)
        {
            // Remove exclusion
            bool result = mod.ExclusionForRequiredMods.Remove(requiredID);

            // If the exclusion is about a required mod, then we need to check for groups, and remove exclusions for both group and group members
            if (result && IsGroupMember(requiredID))
            {
                // SubItem is a group member. Remove exclusion for the group and all members by calling RemoveExclusion with the group ID. This should not cause a loop.
                RemoveExclusionForRequiredMods(mod, GetGroup(requiredID).GroupID);
            }
            else if (result && GroupDictionary.ContainsKey(requiredID))
            {
                // SubItem is a group. Remove exclusion for all group members. This is done manually here and not by calling RemoveExclusion again, to avoid a loop.
                foreach (ulong groupMember in GroupDictionary[requiredID].GroupMembers)
                {
                    mod.ExclusionForRequiredMods.Remove(groupMember);
                }
            }

            return result;
        }


        // Validate a catalog, including counting the number of mods and checking the compatible game version
        private bool Validate()
        {
            // Not valid if Version is 0 or UpdateDate is the lowest value
            if ((Version == 0) || (UpdateDate == DateTime.MinValue))
            {
                Logger.Log($"Catalog { VersionString() } has incorrect version or update date ({ Toolkit.DateString(UpdateDate) }).", 
                    Logger.error);

                return false;
            }

            // Not valid if there are no mods
            if (Mods?.Any() != true)
            {
                Logger.Log($"Catalog { VersionString() } contains no mods.", Logger.error); 
                
                return false;
            }

            // Count the number of mods in the catalog
            Count = Mods.Count;

            // Count the number of mods with a manual review in the catalog
            List<Mod> reviewedMods = Mods.FindAll(x => x.ReviewDate != DateTime.MinValue);

            ReviewCount = reviewedMods?.Any() == null ? 0 : reviewedMods.Count;

            // If the compatible gameversion for the catalog is unknown, try to convert the string field
            if ((CompatibleGameVersion == null) || (CompatibleGameVersion == GameVersion.Unknown))
            {
                CompatibleGameVersion = Toolkit.ConvertToGameVersion(CompatibleGameVersionString);

                if (CompatibleGameVersion == GameVersion.Unknown) 
                {
                    // Conversion failed, assume it's the mods compatible game version
                    CompatibleGameVersion = ModSettings.compatibleGameVersion;
                }
            }

            // Set the version string, so it matches (again) with the version object
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            return true;
        }


        // Prepare a catalog for searching
        internal void CreateIndex()
        {
            // Clear the dictionaries
            ModDictionary.Clear();
            GroupDictionary.Clear();
            AuthorIDDictionary.Clear();
            AuthorURLDictionary.Clear();

            // Add mods to the dictionary
            foreach (Mod mod in Mods) 
            { 
                if (ModDictionary.ContainsKey(mod.SteamID))
                {
                    Logger.Log($"Found a duplicate mod steam ID in the catalog: { mod.SteamID }.", Logger.error);
                }
                else
                {
                    ModDictionary.Add(mod.SteamID, mod);
                }
            }

            // Add groups to the dictionary
            foreach (Group group in Groups) 
            {
                if (GroupDictionary.ContainsKey(group.GroupID))
                {
                    Logger.Log($"Found a duplicate group ID in the catalog: { group.GroupID }.", Logger.error);
                }
                else
                {
                    GroupDictionary.Add(group.GroupID, group);
                }
            }

            // Add authors to one or both of the dictionaries
            foreach (Author author in Authors) 
            { 
                if (author.ProfileID != 0)
                {
                    if (AuthorIDDictionary.ContainsKey(author.ProfileID))
                    {
                        Logger.Log($"Found a duplicate author profile ID in the catalog: { author.ProfileID }.", Logger.error);
                    }
                    else
                    {
                        AuthorIDDictionary.Add(author.ProfileID, author);
                    }                    
                }

                if (!string.IsNullOrEmpty(author.CustomURL))
                {
                    if (AuthorURLDictionary.ContainsKey(author.CustomURL))
                    {
                        Logger.Log($"Found a duplicate author custom URL in the catalog: { author.CustomURL }.", Logger.error);
                    }
                    else
                    {
                        AuthorURLDictionary.Add(author.CustomURL, author);
                    }
                }
            }
        }


        // Save a catalog to disk
        internal bool Save(string fullPath)
        {
            try
            {
                // Write serialized catalog to file
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextWriter writer = new StreamWriter(fullPath))
                {
                    serializer.Serialize(writer, this);
                }

                Logger.Log($"Created catalog { VersionString() } at \"{Toolkit.PrivacyPath(fullPath)}\".");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create catalog at \"{Toolkit.PrivacyPath(fullPath)}\".", Logger.error);

                Logger.Exception(ex);

                return false;
            }
        }


        // Load a catalog from disk and validate it
        internal static Catalog Load(string fullPath)
        {
            // Check if file exists
            if (!File.Exists(fullPath))
            {
                Logger.Log($"Can't load nonexistent catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                return null;
            }

            Catalog catalog = new Catalog();

            try
            {
                // Load catalog from disk
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextReader reader = new StreamReader(fullPath))
                {
                    catalog = (Catalog)serializer.Deserialize(reader);
                }

                // Validate loaded catalog
                if (catalog.Validate())
                {
                    // Valid
                    return catalog;
                }
                else
                {
                    // Invalid, don't return the catalog
                    Logger.Log($"Discarded invalid catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                    return null;
                }
            }
            catch (Exception ex)
            {
                // Loading failed
                if (ex.ToString().Contains("There is an error in XML document") || ex.ToString().Contains("Document element did not appear"))
                {
                    // XML error
                    Logger.Log($"XML error in catalog \"{ Toolkit.PrivacyPath(fullPath) }\". Catalog could not be loaded.", Logger.warning);
                }
                else
                {
                    // Other error
                    Logger.Log($"Can't load catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);
                }

                Logger.Exception(ex);

                return null;
            }
        }
    }
}
