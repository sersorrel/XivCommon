﻿using Dalamud.Game.Text.SeStringHandling;

namespace XivCommon.Functions.Tooltips {
    /// <summary>
    /// The class allowing for action tooltip manipulation
    /// </summary>
    public unsafe class ActionTooltip : BaseTooltip {
        internal ActionTooltip(Tooltips.StringArrayDataSetStringDelegate sadSetString, byte*** stringArrayData, int** numberArrayData) : base(sadSetString, stringArrayData, numberArrayData) {
        }

        /// <summary>
        /// Gets or sets the SeString for the given string enum.
        /// </summary>
        /// <param name="ats">the string to retrieve/update</param>
        public SeString this[ActionTooltipString ats] {
            get => this[(int) ats];
            set => this[(int) ats] = value;
        }

        /// <summary>
        /// Gets or sets which fields are visible on the tooltip.
        /// </summary>
        public ActionTooltipFields Fields {
            get => (ActionTooltipFields) (**(this.NumberArrayData + 4));
            set => **(this.NumberArrayData + 4) = (int) value;
        }
    }
}
