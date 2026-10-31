using System;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Runtime
{
    public static class TickProcessor
    {
        public const double MinDtSeconds = 0.00025;
        public const double MaxDtSeconds = 0.02;
        public const int NeutralSteer = 16384;
        public const int NeutralGas = 0;
        public const int NeutralBrake = 0;
        public static double ClampDt(double dtSeconds, int pollingRate)
        {
            int rate = Math.Clamp(pollingRate > 0 ? pollingRate : 1000, 50, 1000);
            return double.IsFinite(dtSeconds) && dtSeconds > 0 ? Math.Clamp(dtSeconds, MinDtSeconds, MaxDtSeconds) : 1.0 / rate;
        }

        public static bool ComputeGrabDecision(bool lockMouse, bool isGameActive, bool holdMouseUnlocked)
        {
            return lockMouse && isGameActive && !holdMouseUnlocked;
        }

        public static TickOutput ProcessTick(ResolvedProfile profile, bool isGameMonitored, bool isGamePresent, IInputSource inputSource, ICursorLockService cursorLock, MathEngine mathEngine, TickState state, double dtSeconds, IOutputFeeder? feeder = null, bool? isGameFocused = null)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(inputSource);
            ArgumentNullException.ThrowIfNull(cursorLock);
            ArgumentNullException.ThrowIfNull(mathEngine);
            ArgumentNullException.ThrowIfNull(state);
            ;
            dtSeconds = ClampDt(dtSeconds, profile.PollingRate);
            bool isGameActive = !isGameMonitored || isGamePresent;
            if (state.WasGameActive != isGameActive)
            {
                mathEngine.Neutralize();
            }

            state.WasGameActive = isGameActive;
            bool isHubTogglePressed = ControlTickRules.EitherDown(inputSource, profile.KeyToggleHub1, profile.KeyToggleHub2);
            bool hubToggleTriggered = false;
            if (isHubTogglePressed && !state.WasHubTogglePressed)
            {
                hubToggleTriggered = true;
                state.IsHubActive = !state.IsHubActive;
            }

            state.WasHubTogglePressed = isHubTogglePressed;
            bool isLockTogglePressed = ControlTickRules.EitherDown(inputSource, profile.KeyToggleLock1, profile.KeyToggleLock2);
            bool lockToggleTriggered = false;
            if (!state.BypassInternalLockToggle && isLockTogglePressed && !state.WasLockTogglePressed)
            {
                state.LockMouse = !state.LockMouse;
                lockToggleTriggered = true;
            }

            state.WasLockTogglePressed = isLockTogglePressed;
            bool focusSuspended = state.FocusGate.Update(isGameFocused, profile, inputSource, enabled: profile.PauseControlsOnFocusLoss);
            bool isHoldUnlock = ControlTickRules.EitherDown(inputSource, profile.KeyUnlockCursor1, profile.KeyUnlockCursor2);
            state.HoldMouseUnlocked = isHoldUnlock || (profile.ReleaseCursorOnFocusLoss && (focusSuspended || isGameFocused == false));
            ;
            bool shouldLockCursor = ComputeGrabDecision(state.LockMouse, isGameActive, state.HoldMouseUnlocked);
            if (shouldLockCursor && !cursorLock.IsCursorLocked)
            {
                cursorLock.LockCursorAtCurrentPosition();
            }
            else if (!shouldLockCursor && cursorLock.IsCursorLocked)
            {
                cursorLock.UnlockCursor();
            };
            bool isControlSuspended = (!state.LockMouse && profile.CursorUnlockedMode == 2) || focusSuspended || !isGameActive || isHoldUnlock;
            int deltaX = inputSource.ConsumeDeltaX();
            bool isGasPressed = ControlTickRules.EitherDown(inputSource, profile.KeyGas1, profile.KeyGas2);
            bool isBrakePressed = ControlTickRules.EitherDown(inputSource, profile.KeyBrake1, profile.KeyBrake2);
            bool isTurboGas = ControlTickRules.BothDown(inputSource, profile.KeyGas1, profile.KeyGas2);
            bool isTurboBrake = ControlTickRules.BothDown(inputSource, profile.KeyBrake1, profile.KeyBrake2);
            bool centerSteer = ControlTickRules.EitherDown(inputSource, profile.KeyCenterSteering1, profile.KeyCenterSteering2);
            bool isSteerLeft = ControlTickRules.EitherDown(inputSource, profile.KeySteerLeft1, profile.KeySteerLeft2);
            bool isSteerRight = ControlTickRules.EitherDown(inputSource, profile.KeySteerRight1, profile.KeySteerRight2);
            bool shiftDown = ControlTickRules.EitherDown(inputSource, profile.KeyShiftDown1, profile.KeyShiftDown2);
            bool shiftUp = ControlTickRules.EitherDown(inputSource, profile.KeyShiftUp1, profile.KeyShiftUp2);
            int steer;
            int gas;
            int brake;
            bool outShiftDown;
            bool outShiftUp;
            if (isControlSuspended)
            {
                mathEngine.UpdatePedals(false, false, false, false, dtSeconds);
                steer = NeutralSteer;
                gas = NeutralGas;
                brake = NeutralBrake;
                outShiftDown = false;
                outShiftUp = false;
            }
            else
            {
                ;
                if (!profile.UseKeyboardOnlyMode && (state.LockMouse || profile.CursorUnlockedMode == 0) && deltaX != 0)
                {
                    mathEngine.ProcessSteering(deltaX);
                }

                if (profile.UseKeyboardOnlyMode)
                {
                    mathEngine.UpdateKeyboardSteering(isSteerLeft, isSteerRight, dtSeconds);
                }

                if (centerSteer)
                {
                    mathEngine.CenterSteering();
                }

                mathEngine.UpdatePedals(isGasPressed, isBrakePressed, isTurboGas, isTurboBrake, dtSeconds);
                steer = mathEngine.GetMappedSteering();
                var outputs = mathEngine.GetMappedOutputs();
                gas = outputs.gas;
                brake = outputs.brake;
                outShiftDown = shiftDown;
                outShiftUp = shiftUp;
            }

            var buttons = state.VirtualButtons.Process(profile.VirtualButtons, inputSource,
                isGameActive && (!profile.PauseControlsOnFocusLoss || isGameFocused == true) && state.IsHubActive && !ControlTickRules.EitherDown(inputSource, profile.KeyUnlockCursor1, profile.KeyUnlockCursor2), true, dtSeconds, feeder?.TestCapabilities.ButtonCount ?? 128);
            buttons = new OutputButtons(buttons.Low | OutputButtons.Gears(outShiftDown, outShiftUp).Low, buttons.High);
            if (buttons.High == 0 && (buttons.Low & ~3UL) == 0)
                feeder?.UpdateAxesAndButtons(steer, gas, brake, outShiftDown, outShiftUp);
            else feeder?.UpdateReport(new(steer, gas, brake, buttons));
            return new TickOutput(steer, gas, brake, outShiftDown, outShiftUp, isControlSuspended, hubToggleTriggered, lockToggleTriggered);
        }
    }
}
