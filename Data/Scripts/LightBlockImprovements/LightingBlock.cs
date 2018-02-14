using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.InteriorLightAccess
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorLight), false)]
    public class InteriorLight : LightingBlock { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ReflectorLight), false)]
    public class Spotlight : LightingBlock { }

    public class LightingBlock : MyGameLogicComponent
    {
        private IMyLightingBlock block;

        private Color bulbColor;
        private float intensity;
        private bool lightOn;
        private bool blinkOn;
        private float currentIntensity;
        private float currentLightPower;

        private const float LIGHT_FADE_SPEED = 0.05f;
        private const double UPDATE_VIEW_DIST_SQ = 3000 * 3000;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyLightingBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            block.IsWorkingChanged += UpdateSettings;
            block.PropertiesChanged += UpdateSettings;
            UpdateSettings(block);
        }

        public override void Close()
        {
            block.IsWorkingChanged -= UpdateSettings;
            block.PropertiesChanged -= UpdateSettings;
        }
        
        private void UpdateSettings(IMyCubeBlock _NotUsed)
        {
            try
            {
                if(Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, block.WorldMatrix.Translation) > UPDATE_VIEW_DIST_SQ)
                    return; // ignore lights that are too far away

                NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;

                var def = (MyLightingBlockDefinition)block.SlimBlock.BlockDefinition;
                intensity = (block.Intensity / def.LightIntensity.Max);

                var colorIntensity = Math.Max(0.5f * intensity, 0.1f);
                bulbColor = block.Color * colorIntensity;

                UpdateIntensity();
                UpdateEnabled();
                UpdateEmissivity(false);
            }
            catch(Exception e)
            {
                MyLog.Default.WriteLine(e);
                MyAPIGateway.Utilities.ShowNotification($"[ Error in {GetType().FullName}: {e.Message} ]", 10000, MyFontEnum.Red);
            }
        }

        // HACK blink and fade logic copied from game source

        public override void UpdateAfterSimulation100()
        {
            if((block.BlinkIntervalSeconds > 0f) || (GetNewLightPower() != currentLightPower))
            {
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            else
            {
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                float newLightPower = GetNewLightPower();

                if(newLightPower != currentLightPower)
                {
                    currentLightPower = newLightPower;
                    UpdateIntensity();
                }

                UpdateLightBlink();
                UpdateEnabled();
                UpdateEmissivity(false);
            }
            catch(Exception e)
            {
                MyLog.Default.WriteLine(e);
                MyAPIGateway.Utilities.ShowNotification($"[ Error in {GetType().FullName}: {e.Message} ]", 10000, MyFontEnum.Red);
            }
        }
        
        private float GetNewLightPower()
        {
            return MathHelper.Clamp(currentLightPower + (float)(block.IsWorking ? 1 : -1) * LIGHT_FADE_SPEED, 0f, 1f);
        }

        private void UpdateLightBlink()
        {
            if(block.BlinkIntervalSeconds > 0.00099f)
            {
                var gameTime = (MyAPIGateway.Session.GameDateTime - new DateTime(2081, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                ulong num = (ulong)(block.BlinkIntervalSeconds * 1000f);
                float num2 = num * block.BlinkOffset * 0.01f;
                ulong num3 = (ulong)(gameTime.TotalMilliseconds - (double)num2);
                ulong num4 = num3 % num;
                ulong num5 = (ulong)(num * block.BlinkLength * 0.01f);
                blinkOn = (num5 > num4);
                return;
            }

            blinkOn = true;
        }

        private void UpdateIntensity()
        {
            currentIntensity = currentLightPower * intensity;
        }

        private void UpdateEnabled()
        {
            lightOn = currentLightPower * intensity > 0f && blinkOn;
        }

        private void UpdateEmissivity(bool force)
        {
            var setIntensity = (lightOn ? currentIntensity : 0);

            block.SetEmissiveParts("Bulb", bulbColor, setIntensity);

            if(block is IMyReflectorLight)
            {
                var reflectorColor = (setIntensity > 0 ? bulbColor : Color.White);
                block.SetEmissiveParts("Reflector", reflectorColor, setIntensity);
            }
        }
    }
}