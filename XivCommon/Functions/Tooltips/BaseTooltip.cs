﻿using System;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivCommon.Functions.Tooltips;

/// <summary>
/// The base class for tooltips
/// </summary>
public abstract unsafe class BaseTooltip {
    /// <summary>
    /// A pointer to the StringArrayData class for this tooltip.
    /// </summary>
    private readonly byte*** _stringArrayData; // this is StringArrayData* when ClientStructs is updated

    /// <summary>
    /// A pointer to the NumberArrayData class for this tooltip.
    /// </summary>
    protected readonly int** NumberArrayData;

    internal BaseTooltip(byte*** stringArrayData, int** numberArrayData) {
        this._stringArrayData = stringArrayData;
        this.NumberArrayData = numberArrayData;
    }

    /// <summary>
    /// <para>
    /// Gets the SeString at the given index for this tooltip.
    /// </para>
    /// <para>
    /// Implementors should provide an enum accessor for this.
    /// </para>
    /// </summary>
    /// <param name="index">string index to retrieve</param>
    protected SeString this[int index] {
        get {
            var ptr = *(this._stringArrayData + 4) + index;
            return Util.ReadSeString((IntPtr) (*ptr));
        }
        set {
            var encoded = value.Encode().Terminate();

            fixed (byte* encodedPtr = encoded) {
                ((StringArrayData*) this._stringArrayData)->SetValue(index, encodedPtr, false, true, true);
            }
        }
    }
}
