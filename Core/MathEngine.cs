using System;
using SimRacingHub.Models;

namespace SimRacingHub.Core
{
    public class MathEngine
    {
        private readonly ResolvedProfile _profile;
        private readonly AxisState _state;
        
        public MathEngine(ResolvedProfile profile, AxisState state)
        {
            _profile = profile;
            _state = state;
        }

        public void ProcessSteering(int deltaX, TelemetryData telemetry)
        {
            if (_profile.UseKeyboardOnlyMode) return;

            double mouseSens = _profile.MouseSensitivity * telemetry.MouseSensMultiplier;
            _state.Steering += deltaX * mouseSens;
            
            double steerMaxLimit = 32768.0;
            double steerMinLimit = -32768.0;

            if (_profile.UseSteeringLockClamp && _profile.GlobalSteeringRange > 0)
            {
                double ratio = (double)_profile.CarSteeringLock / _profile.GlobalSteeringRange;
                steerMaxLimit = 32768.0 * ratio;
                steerMinLimit = -32768.0 * ratio;
            }

            // Clamp
            if (_state.Steering > steerMaxLimit) _state.Steering = steerMaxLimit;
            else if (_state.Steering < steerMinLimit) _state.Steering = steerMinLimit;
        }

        public void UpdateKeyboardSteering(bool isSteerLeft, bool isSteerRight, double dtSeconds)
        {
            if (!_profile.UseKeyboardOnlyMode) return;

            double targetSteer = 0.0;
            if (isSteerLeft && !isSteerRight)
            {
                targetSteer = -32768.0;
            }
            else if (isSteerRight && !isSteerLeft)
            {
                targetSteer = 32768.0;
            }

            if (_profile.UseSteeringLockClamp && _profile.GlobalSteeringRange > 0)
            {
                double ratio = (double)_profile.CarSteeringLock / _profile.GlobalSteeringRange;
                targetSteer *= ratio;
            }

            if (_profile.KeySteerSnapToFullLock)
            {
                _state.Steering = targetSteer;
                return;
            }

            double rampUpTime = Math.Max(0.01, _profile.KeySteerRampUpMs / 1000.0);
            double rampDownTime = Math.Max(0.01, _profile.KeySteerRampDownMs / 1000.0);

            double maxRange = 32768.0;
            if (_profile.UseSteeringLockClamp && _profile.GlobalSteeringRange > 0)
            {
                double ratio = (double)_profile.CarSteeringLock / _profile.GlobalSteeringRange;
                if (ratio > 0) maxRange *= ratio;
            }

            double rampUpSpeed = maxRange / rampUpTime;
            double rampDownSpeed = maxRange / rampDownTime;

            double current = _state.Steering;

            if (targetSteer == 0.0)
            {
                if (current > 0)
                {
                    current = Math.Max(0.0, current - rampDownSpeed * dtSeconds);
                }
                else if (current < 0)
                {
                    current = Math.Min(0.0, current + rampDownSpeed * dtSeconds);
                }
            }
            else if (targetSteer > 0)
            {
                if (current < 0)
                {
                    current = Math.Min(targetSteer, current + rampDownSpeed * dtSeconds);
                }
                else
                {
                    current = Math.Min(targetSteer, current + rampUpSpeed * dtSeconds);
                }
            }
            else // targetSteer < 0
            {
                if (current > 0)
                {
                    current = Math.Max(targetSteer, current - rampDownSpeed * dtSeconds);
                }
                else
                {
                    current = Math.Max(targetSteer, current - rampUpSpeed * dtSeconds);
                }
            }

            _state.Steering = current;
        }

        public void CenterSteering()
        {
            _state.Steering = 0;
        }

        public void UpdatePedals(bool isGasPressed, bool isBrakePressed, bool isTurboGas, bool isTurboBrake, TelemetryData telemetry)
        {
            UpdateBrake(isBrakePressed, isTurboBrake, isTurboGas, telemetry);
            UpdateGas(isGasPressed, isBrakePressed, isTurboGas);
        }

        private void UpdateBrake(bool isBrakePressed, bool isTurboBrake, bool isTurboGas, TelemetryData telemetry)
        {
            double fullRange = 65536.0;
            double steeringMin = -32768.0;
            double steeringMax = 32768.0;

            // Trail braking math logic stripped in open-source version
            double brakeLimitVal = steeringMax;

            double currentBrakePct = (_state.BrakeVal - steeringMin) / fullRange;

            if (isBrakePressed)
            {
                if (currentBrakePct < _profile.BrakeThreshold)
                {
                    _state.BrakeVal += _profile.BrakeAttackFast;
                }
                else
                {
                    _state.BrakeVal += _profile.BrakeAttackSlow;
                }

                if (!isTurboBrake && _state.BrakeVal > brakeLimitVal)
                {
                    _state.BrakeVal = brakeLimitVal;
                }
            }
            else
            {
                _state.BrakeVal -= _profile.BrakeDecay;
            }

            if (!isTurboGas && !isTurboBrake)
            {
                if (_state.BrakeVal > brakeLimitVal)
                {
                    _state.BrakeVal = Math.Max(brakeLimitVal, _state.BrakeVal - _profile.TbDecay);
                }
            }

            if (_state.BrakeVal > steeringMax) _state.BrakeVal = steeringMax;
            else if (_state.BrakeVal < steeringMin) _state.BrakeVal = steeringMin;
        }

        private void UpdateGas(bool isGasPressed, bool isBrakePressed, bool isTurboGas)
        {
            double fullRange = 65536.0;
            double steeringMin = -32768.0;
            double steeringMax = 32768.0;

            bool shouldApplyGas = isBrakePressed ? isTurboGas : isGasPressed;
            double currentGasPct = (_state.GasVal - steeringMin) / fullRange;

            if (shouldApplyGas)
            {
                if (isTurboGas)
                {
                    _state.GasVal += _profile.GasAttackTurbo;
                }
                else if (currentGasPct < _profile.GasThreshold)
                {
                    _state.GasVal += _profile.GasAttackFast;
                }
                else
                {
                    _state.GasVal += _profile.GasAttackSlow;
                }
            }
            else
            {
                if (isBrakePressed)
                {
                    _state.GasVal -= _profile.GasInstantCut;
                }
                else
                {
                    _state.GasVal -= _profile.GasDecay;
                }
            }

            if (_state.GasVal > steeringMax) _state.GasVal = steeringMax;
            else if (_state.GasVal < steeringMin) _state.GasVal = steeringMin;
        }

        public int MapAxis(double val, double inMin, double inMax, double outMin, double outMax)
        {
            return (int)Math.Round(outMin + ((val - inMin) / (inMax - inMin)) * (outMax - outMin));
        }

        public (int steer, int gas, int brake) GetMappedOutputs(TelemetryData telemetry)
        {
            // Steering Gamma
            double rawSteerNorm = _state.Steering / 32768.0;
            if (rawSteerNorm > 1.0) rawSteerNorm = 1.0;
            else if (rawSteerNorm < -1.0) rawSteerNorm = -1.0;

            double steerGamma = _profile.SteeringGamma <= 0 ? 1.0 : _profile.SteeringGamma;
            double curvedSteerNorm = Math.Sign(rawSteerNorm) * Math.Pow(Math.Abs(rawSteerNorm), steerGamma);
            double finalSteering = (curvedSteerNorm * 32768.0) + _profile.SteeringOffset;
            if (finalSteering > 32768) finalSteering = 32768;
            else if (finalSteering < -32768) finalSteering = -32768;

            int mappedSteer = MapAxis(finalSteering, -32768, 32768, _profile.OutSteerMin, _profile.OutSteerMax);

            // Gas Gamma
            double rawGasNorm = (_state.GasVal - (-32768.0)) / 65536.0;
            if (rawGasNorm > 1.0) rawGasNorm = 1.0;
            else if (rawGasNorm < 0.0) rawGasNorm = 0.0;

            double gasGamma = _profile.GasGamma <= 0 ? 1.0 : _profile.GasGamma;
            double curvedGasNorm = Math.Pow(rawGasNorm, gasGamma);
            double curvedGasVal = -32768.0 + (curvedGasNorm * 65536.0);

            double rawGas = MapAxis(curvedGasVal, -32768, 32768, _profile.OutGasMin, _profile.OutGasMax);
            int mappedGas;
            if (_profile.UseTcHelper)
            {
                mappedGas = (int)Math.Round(_profile.OutGasMin + (rawGas - _profile.OutGasMin) * telemetry.TcScaleFactor);
            }
            else
            {
                mappedGas = (int)Math.Round(rawGas);
            }
            
            // Brake Gamma
            double rawBrakeNorm = (_state.BrakeVal - (-32768.0)) / 65536.0;
            if (rawBrakeNorm > 1.0) rawBrakeNorm = 1.0;
            else if (rawBrakeNorm < 0.0) rawBrakeNorm = 0.0;

            double brakeGamma = _profile.BrakeGamma <= 0 ? 1.0 : _profile.BrakeGamma;
            double curvedBrakeNorm = Math.Pow(rawBrakeNorm, brakeGamma);
            double curvedBrakeVal = -32768.0 + (curvedBrakeNorm * 65536.0);

            double rawBrake = MapAxis(curvedBrakeVal, -32768, 32768, _profile.OutBrakeMin, _profile.OutBrakeMax);
            int mappedBrake;

            if (_profile.UseAbsHelper)
            {
                mappedBrake = (int)Math.Round(_profile.OutBrakeMin + (rawBrake - _profile.OutBrakeMin) * telemetry.ScaleFactor);
            }
            else
            {
                mappedBrake = (int)Math.Round(rawBrake);
            }

            if (_profile.InvertSteering)
            {
                mappedSteer = _profile.OutSteerMax - (mappedSteer - _profile.OutSteerMin);
            }
            
            if (_profile.InvertGas)
            {
                mappedGas = _profile.OutGasMax - (mappedGas - _profile.OutGasMin);
            }
            
            if (_profile.InvertBrake)
            {
                mappedBrake = _profile.OutBrakeMax - (mappedBrake - _profile.OutBrakeMin);
            }

            return (mappedSteer, mappedGas, mappedBrake);
        }
    }
}
