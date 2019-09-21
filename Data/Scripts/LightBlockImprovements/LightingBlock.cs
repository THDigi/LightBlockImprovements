using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.LightBlockImprovements
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorLight), false)]
    public class InteriorLight : LightingBlock { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ReflectorLight), false)]
    public class Spotlight : LightingBlock { }

    public class LightingBlock : MyGameLogicComponent
    {
        private IMyLightingBlock block;
        private MyLightingBlockDefinition def;
        private Color bulbColor;
        private float intensity = 0;
        private bool lightOn = true;
        private bool blinkOn = true;
        private float lightPower = 0;

        private const float LIGHT_FADE_SPEED = 0.05f; // must match MyLightingBlock.m_lightTurningOnSpeed
        private const double UPDATE_VIEW_DIST = 3000; // rectangular distance

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyLightingBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(block?.CubeGrid?.Physics == null)
                    return;

                def = (MyLightingBlockDefinition)block.SlimBlock.BlockDefinition;

                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

                block.IsWorkingChanged += WorkingChanged;
                block.PropertiesChanged += UpdateSettings;
                UpdateSettings(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Close()
        {
            try
            {
                if(block == null)
                    return;

                block.IsWorkingChanged -= WorkingChanged;
                block.PropertiesChanged -= UpdateSettings;
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private bool InViewRange => (MyAPIGateway.Session.Camera.WorldMatrix.Translation - block.WorldMatrix.Translation).AbsMax() <= UPDATE_VIEW_DIST;

        private float GetNewLightPower => MathHelper.Clamp(lightPower + (block.IsWorking ? 1 : -1) * LIGHT_FADE_SPEED, 0f, 1f);

        private void WorkingChanged(IMyCubeBlock _)
        {
            UpdateSettings(block);
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        private void UpdateSettings(IMyCubeBlock _)
        {
            try
            {
                intensity = (block.Intensity / def.LightIntensity.Max);

                var colorIntensity = Math.Max(0.5f * intensity, 0.3f);
                bulbColor = block.Color * colorIntensity;

                lightPower = GetNewLightPower;
                UpdateEnabled();
                UpdateEmissivity();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        // simulating the exact blink and fade logic as vanilla for the emissives to match the light behavior.
        public override void UpdateAfterSimulation100()
        {
            try
            {
                if(!InViewRange)
                {
                    NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
                    return;
                }

                UpdateSettings(block);

                if(block.BlinkIntervalSeconds > 0.00099f || Math.Abs(GetNewLightPower - lightPower) > 0)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                }
                else
                {
                    NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                lightPower = GetNewLightPower;
                UpdateLightBlink();
                UpdateEnabled();
                UpdateEmissivity();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void UpdateLightBlink()
        {
            if(block.BlinkIntervalSeconds > 0.00099f)
            {
                var gameTime = (MyAPIGateway.Session.GameDateTime - new DateTime(2081, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                double blinkIntervalMs = block.BlinkIntervalSeconds * 1000;
                double n1 = blinkIntervalMs * block.BlinkOffset * 0.01f;
                ulong n2 = (ulong)(gameTime.TotalMilliseconds - n1) % (ulong)blinkIntervalMs;
                ulong n3 = (ulong)(blinkIntervalMs * block.BlinkLength * 0.01f);
                blinkOn = (n3 > n2);
            }
            else
            {
                blinkOn = true;
            }
        }

        private void UpdateEnabled()
        {
            lightOn = (blinkOn && (lightPower * intensity) > 0f);
        }

        private void UpdateEmissivity()
        {
            if(!InViewRange)
                return;

            var setIntensity = (lightOn ? (lightPower * intensity) : 0);
            block.SetEmissiveParts("Bulb", bulbColor, setIntensity);

            if(block is IMyReflectorLight)
            {
                var reflectorColor = (setIntensity > 0 ? bulbColor : Color.White);
                block.SetEmissiveParts("Reflector", reflectorColor, setIntensity);
            }
        }
    }
}