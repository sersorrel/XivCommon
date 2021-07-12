﻿using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Lumina.Excel.GeneratedSheets;

namespace XivCommon.Functions {
    /// <summary>
    /// Duty Finder functions
    /// </summary>
    public class DutyFinder {
        private static class Signatures {
            internal const string OpenRegularDuty = "48 89 6C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B F9 41 0F B6 E8";
            internal const string OpenRoulette = "E9 ?? ?? ?? ?? 8B 93 ?? ?? ?? ?? 48 83 C4 20";
        }

        private const uint ContentsFinderAgentId = 50;

        private delegate IntPtr OpenDutyDelegate(IntPtr agent, uint contentFinderCondition, byte a3);

        private delegate IntPtr OpenRouletteDelegate(IntPtr agent, byte roulette, byte a3);

        private GameFunctions Functions { get; }
        private readonly OpenDutyDelegate? _openDuty;
        private readonly OpenRouletteDelegate? _openRoulette;

        internal DutyFinder(GameFunctions functions, SigScanner scanner) {
            this.Functions = functions;

            if (scanner.TryScanText(Signatures.OpenRegularDuty, out var openDutyPtr, "Duty Finder (open duty)")) {
                this._openDuty = Marshal.GetDelegateForFunctionPointer<OpenDutyDelegate>(openDutyPtr);
            }

            if (scanner.TryScanText(Signatures.OpenRoulette, out var openRoulettePtr, "Duty Finder (open roulette)")) {
                this._openRoulette = Marshal.GetDelegateForFunctionPointer<OpenRouletteDelegate>(openRoulettePtr);
            }
        }

        /// <summary>
        /// Opens the Duty Finder to the given duty.
        /// </summary>
        /// <param name="condition">duty to show</param>
        public void OpenDuty(ContentFinderCondition condition) {
            this.OpenDuty(condition.RowId);
        }

        /// <summary>
        /// Opens the Duty Finder to the given duty ID.
        /// </summary>
        /// <param name="contentFinderCondition">ID of duty to show</param>
        public void OpenDuty(uint contentFinderCondition) {
            if (this._openDuty == null) {
                throw new InvalidOperationException("Could not find signature for open duty function");
            }

            var agent = this.Functions.GetAgentByInternalId(ContentsFinderAgentId);

            this._openDuty(agent, contentFinderCondition, 0);
        }

        /// <summary>
        /// Opens the Duty Finder to the given roulette.
        /// </summary>
        /// <param name="roulette">roulette to show</param>
        public void OpenRoulette(ContentRoulette roulette) {
            this.OpenRoulette((byte) roulette.RowId);
        }

        /// <summary>
        /// Opens the Duty Finder to the given roulette ID.
        /// </summary>
        /// <param name="roulette">ID of roulette to show</param>
        public void OpenRoulette(byte roulette) {
            if (this._openRoulette == null) {
                throw new InvalidOperationException("Could not find signature for open roulette function");
            }

            var agent = this.Functions.GetAgentByInternalId(ContentsFinderAgentId);

            this._openRoulette(agent, roulette, 0);
        }
    }
}
