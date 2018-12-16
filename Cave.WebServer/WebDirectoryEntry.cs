using System;

namespace Cave.Web
{
    /// <summary>
    /// Provides all metadata of a web directory entry (file, subdirectory)
    /// </summary>
    public struct WebDirectoryEntry
    {
        /// <summary>The type</summary>
        public WebDirectoryEntryType Type;

        /// <summary>The name</summary>
        public string Name;

        /// <summary>The (last modification) date time</summary>
        public DateTime DateTime;

        /// <summary>The size</summary>
        public long Size;

        /// <summary>The owner</summary>
        public string Owner;

        /// <summary>The group</summary>
        public string Group;

        /// <summary>The permissions</summary>
        public int Permissions;

        /// <summary>The link to the file</summary>
        public string Link;
    }
}
