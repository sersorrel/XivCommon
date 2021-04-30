﻿namespace XivCommon.Functions.ContextMenu {
    /// <summary>
    /// A custom context menu item
    /// </summary>
    public class ContextMenuItem : BaseContextMenuItem {
        /// <summary>
        /// The name of the context item to be shown for English clients.
        /// </summary>
        public string NameEnglish { get; set; }

        /// <summary>
        /// The name of the context item to be shown for Japanese clients.
        /// </summary>
        public string NameJapanese { get; set; }

        /// <summary>
        /// The name of the context item to be shown for French clients.
        /// </summary>
        public string NameFrench { get; set; }

        /// <summary>
        /// The name of the context item to be shown for German clients.
        /// </summary>
        public string NameGerman { get; set; }

        /// <summary>
        /// The action to perform when this item is clicked.
        /// </summary>
        public ContextMenu.ContextMenuItemSelectedDelegate Action { get; set; }

        /// <summary>
        /// Create a new context menu item.
        /// </summary>
        /// <param name="name">the English name of the item, copied to other languages</param>
        /// <param name="action">the action to perform on click</param>
        public ContextMenuItem(string name, ContextMenu.ContextMenuItemSelectedDelegate action) {
            this.NameEnglish = name;
            this.NameJapanese = name;
            this.NameFrench = name;
            this.NameGerman = name;

            this.Action = action;
        }
    }
}
