﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace XivCommon.Functions {
    /// <summary>
    /// Context menu functions
    /// </summary>
    public class ContextMenu : IDisposable {
        private static class Signatures {
            internal const string ContextMenuOpen = "48 8B C4 57 41 56 41 57 48 81 EC ?? ?? ?? ??";
            internal const string ContextMenuSelected = "48 89 5C 24 ?? 55 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 80 B9 ?? ?? ?? ?? ??";
            internal const string AtkValueChangeType = "E8 ?? ?? ?? ?? 45 84 F6 48 8D 4C 24 ??";
            internal const string AtkValueSetString = "E8 ?? ?? ?? ?? 41 03 ED";
            internal const string GetAddonByInternalId = "E8 ?? ?? ?? ?? 8B 6B 20";
        }

        /// <summary>
        /// Offset from addon to menu type
        /// </summary>
        private const int ParentAddonIdOffset = 0x1D2;

        /// <summary>
        /// Offset from agent to actions byte array pointer (have to add the actions offset after)
        /// </summary>
        private const int MenuActionsPointerOffset = 0xD18;

        /// <summary>
        /// Offset from agent to actions byte array
        /// </summary>
        private const int MenuActionsOffset = 0x428;

        private const int NoopContextId = 0x67;

        private unsafe delegate byte ContextMenuOpenDelegate(IntPtr addon, int menuSize, AtkValue* atkValueArgs);

        private delegate IntPtr GetAddonByInternalIdDelegate(IntPtr raptureAtkUnitManager, short id);

        private readonly GetAddonByInternalIdDelegate _getAddonByInternalId = null!;

        private Hook<ContextMenuOpenDelegate>? ContextMenuOpenHook { get; }

        private delegate byte ContextMenuItemSelectedDelegate(IntPtr addon, int index, byte a3);

        private Hook<ContextMenuItemSelectedDelegate>? ContextMenuItemSelectedHook { get; }

        private unsafe delegate void AtkValueChangeTypeDelegate(AtkValue* thisPtr, ValueType type);

        private readonly AtkValueChangeTypeDelegate _atkValueChangeType = null!;

        private unsafe delegate void AtkValueSetStringDelegate(AtkValue* thisPtr, byte* bytes);

        private readonly AtkValueSetStringDelegate _atkValueSetString = null!;

        private GameFunctions Functions { get; }
        private ClientLanguage Language { get; }
        private Dictionary<string, List<ContextMenuItem>> Items { get; } = new();
        private int NormalSize { get; set; }

        internal ContextMenu(GameFunctions functions, SigScanner scanner, ClientLanguage language) {
            this.Functions = functions;
            this.Language = language;

            if (scanner.TryScanText(Signatures.AtkValueChangeType, out var changeTypePtr, "Context Menu (change type)")) {
                this._atkValueChangeType = Marshal.GetDelegateForFunctionPointer<AtkValueChangeTypeDelegate>(changeTypePtr);
            } else {
                return;
            }

            if (scanner.TryScanText(Signatures.AtkValueSetString, out var setStringPtr, "Context Menu (set string)")) {
                this._atkValueSetString = Marshal.GetDelegateForFunctionPointer<AtkValueSetStringDelegate>(setStringPtr);
            } else {
                return;
            }

            if (scanner.TryScanText(Signatures.GetAddonByInternalId, out var getAddonPtr, "Context Menu (get addon)")) {
                this._getAddonByInternalId = Marshal.GetDelegateForFunctionPointer<GetAddonByInternalIdDelegate>(getAddonPtr);
            } else {
                return;
            }

            if (scanner.TryScanText(Signatures.ContextMenuOpen, out var openPtr, "Context Menu open")) {
                unsafe {
                    this.ContextMenuOpenHook = new Hook<ContextMenuOpenDelegate>(openPtr, new ContextMenuOpenDelegate(this.OpenMenuDetour));
                }

                this.ContextMenuOpenHook.Enable();
            } else {
                return;
            }

            if (scanner.TryScanText(Signatures.ContextMenuSelected, out var selectedPtr, "Context Menu selected")) {
                this.ContextMenuItemSelectedHook = new Hook<ContextMenuItemSelectedDelegate>(selectedPtr, new ContextMenuItemSelectedDelegate(this.ItemSelectedDetour));
                this.ContextMenuItemSelectedHook.Enable();
            }
        }

        /// <inheritdoc />
        public void Dispose() {
            this.ContextMenuOpenHook?.Dispose();
            this.ContextMenuItemSelectedHook?.Dispose();
        }

        private IntPtr GetContextMenuAgent() {
            return this.Functions.GetAgentByInternalId(9);
        }

        private unsafe string GetParentAddonName(IntPtr addon) {
            var parentAddonId = Marshal.ReadInt16(addon + ParentAddonIdOffset);
            var stage = (AtkStage*) this.Functions.GetAtkStageSingleton();
            var parentAddon = this._getAddonByInternalId((IntPtr) stage->RaptureAtkUnitManager, parentAddonId);
            return Encoding.UTF8.GetString(Util.ReadTerminated(parentAddon + 8));
        }

        private unsafe byte OpenMenuDetour(IntPtr addon, int menuSize, AtkValue* atkValueArgs) {
            this.NormalSize = menuSize - 7;

            var addonName = this.GetParentAddonName(addon);

            var agent = this.GetContextMenuAgent();

            if (!this.Items.TryGetValue(addonName, out var registered)) {
                goto Original;
            }

            foreach (var item in registered) {
                // set up the agent to ignore this item
                var menuActions = (byte*) (Marshal.ReadIntPtr(agent + MenuActionsPointerOffset) + MenuActionsOffset);
                *(menuActions + menuSize) = NoopContextId;

                // set up the new menu item
                var newItem = &atkValueArgs[menuSize];
                this._atkValueChangeType(newItem, ValueType.String);
                var name = this.Language switch {
                    ClientLanguage.Japanese => item.NameJapanese,
                    ClientLanguage.English => item.NameEnglish,
                    ClientLanguage.German => item.NameGerman,
                    ClientLanguage.French => item.NameFrench,
                    _ => throw new ArgumentOutOfRangeException(),
                };
                var nameBytes = Encoding.UTF8.GetBytes(name).Terminate();
                fixed (byte* nameBytesPtr = nameBytes) {
                    this._atkValueSetString(newItem, nameBytesPtr);
                }

                // increment the menu size
                menuSize += 1;
                (&atkValueArgs[0])->UInt += 1;
            }

            Original:
            return this.ContextMenuOpenHook!.Original(addon, menuSize, atkValueArgs);
        }

        private byte ItemSelectedDetour(IntPtr addon, int index, byte a3) {
            var addonName = this.GetParentAddonName(addon);

            // a custom item is being clicked
            if (index >= this.NormalSize) {
                if (!this.Items.TryGetValue(addonName, out var registered)) {
                    goto Original;
                }

                var idx = index - this.NormalSize;
                if (registered.Count <= idx) {
                    goto Original;
                }

                var item = registered[idx];
                try {
                    item.Action();
                } catch (Exception ex) {
                    PluginLog.LogError(ex, "Exception in custom context menu item");
                }
            }

            Original:
            return this.ContextMenuItemSelectedHook!.Original(addon, index, a3);
        }

        /// <summary>
        /// Register a menu item to appear in a context menu.
        /// </summary>
        /// <param name="addon">the addon to show the item in</param>
        /// <param name="item">the item to be shown</param>
        public void RegisterAction(string addon, ContextMenuItem item) {
            if (!this.Items.TryGetValue(addon, out var registered)) {
                this.Items[addon] = new List<ContextMenuItem>();
                registered = this.Items[addon];
            }

            registered.Add(item);
        }

        /// <summary>
        /// Remove a previously-registered context menu item.
        /// </summary>
        /// <param name="addon">the addon the item was registered under</param>
        /// <param name="item">the item to be removed</param>
        public void UnregisterAction(string addon, ContextMenuItem item) {
            this.UnregisterAction(addon, item.Id);
        }

        /// <summary>
        /// Remove a previously-registered context menu item.
        /// </summary>
        /// <param name="addon">the addon the item was registered under</param>
        /// <param name="id">the id of the item to be removed</param>
        public void UnregisterAction(string addon, Guid id) {
            if (!this.Items.TryGetValue(addon, out var registered)) {
                return;
            }

            registered.RemoveAll(item => item.Id == id);
        }
    }

    /// <summary>
    /// A custom context menu item
    /// </summary>
    public class ContextMenuItem {
        /// <summary>
        /// A unique ID to identify this item.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The name of the context item to be shown for English clients.
        /// </summary>
        public string NameEnglish { get; }

        /// <summary>
        /// The name of the context item to be shown for Japanese clients.
        /// </summary>
        public string NameJapanese { get; }

        /// <summary>
        /// The name of the context item to be shown for French clients.
        /// </summary>
        public string NameFrench { get; }

        /// <summary>
        /// The name of the context item to be shown for German clients.
        /// </summary>
        public string NameGerman { get; }

        /// <summary>
        /// The action to perform when this item is clicked.
        /// </summary>
        public Action Action { get; }

        /// <summary>
        /// Create a new context menu item.
        /// </summary>
        /// <param name="name">the English name of the item, copied to other languages</param>
        /// <param name="action">the action to perform on click</param>
        public ContextMenuItem(string name, Action action) {
            this.NameEnglish = name;
            this.NameJapanese = name;
            this.NameFrench = name;
            this.NameGerman = name;

            this.Action = action;
        }
    }
}
