using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.InteriorLightAccess
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class InteriorLightAccess : MySessionComponentBase
    {
        private readonly Dictionary<MyStringHash, string> replaceIds = new Dictionary<MyStringHash, string>()
        {
            [MyStringHash.GetOrCompute("SmallLight2")] = "SmallBlockSmallLight",
        };

        private readonly HashSet<IMySlimBlock> replaceBlocks = new HashSet<IMySlimBlock>();

        public override void LoadData()
        {
            MyEntities.OnEntityAdd += OnEntityAdded;

            MyLightingBlockDefinition def;

            def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "SmallLight", @"Models\LargeInteriorLight.mwm");
            if(def != null)
            {
                def.LightFalloff.Min = 0.5f;
                def.LightIntensity.Min = 0.5f;
            }

            def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "SmallBlockSmallLight", @"Models\SmallInteriorLight.mwm");
            if(def != null)
            {
                def.LightFalloff.Min = 0.5f;
                def.LightIntensity.Min = 0.5f;
            }

            def = GetDefAndSetModel(typeof(MyObjectBuilder_ReflectorLight), "LargeBlockFrontLight", @"Models\LargeSpotlight.mwm");
            def = GetDefAndSetModel(typeof(MyObjectBuilder_ReflectorLight), "SmallBlockFrontLight", @"Models\SmallSpotlight.mwm");

            //def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_1corner", @"Models\LargeCornerLight.mwm");
            //def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_1corner", @"Models\SmallCornerLight.mwm");

            //def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_2corner", @"Models\LargeDoubleCornerLight.mwm");
            //def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_2corner", @"Models\SmallDoubleCornerLight.mwm");
        }

        protected override void UnloadData()
        {
            MyEntities.OnEntityAdd -= OnEntityAdded;
        }

        private MyLightingBlockDefinition GetDefAndSetModel(Type type, string subtypeId, string model)
        {
            MyCubeBlockDefinition def;

            if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(type, MyStringHash.GetOrCompute(subtypeId)), out def))
            {
                var lightDef = (MyLightingBlockDefinition)def;
                lightDef.Model = Path.Combine(ModContext.ModPath, model);
                return lightDef;
            }

            return null;
        }

        private void OnEntityAdded(MyEntity ent)
        {
            // because all the MyAPIGateway stuff are null in LoadData()
            if(MyAPIGateway.Session != null && !MyAPIGateway.Session.IsServer)
            {
                MyEntities.OnEntityAdd -= OnEntityAdded;
                return;
            }

            var grid = ent as MyCubeGrid;

            if(grid == null)
                return;

            replaceBlocks.Clear();

            var blocks = grid.GetBlocks();

            foreach(IMySlimBlock b in blocks)
            {
                if(replaceIds.ContainsKey(b.BlockDefinition.Id.SubtypeId))
                {
                    replaceBlocks.Add(b);
                }
            }

            var gridModAPI = (IMyCubeGrid)ent;

            foreach(var b in replaceBlocks)
            {
                var obj = b.GetObjectBuilder(false);
                obj.SubtypeName = replaceIds[b.BlockDefinition.Id.SubtypeId];
                obj.BlockOrientation = new MyBlockOrientation(Base6Directions.GetOppositeDirection(obj.BlockOrientation.Up), obj.BlockOrientation.Forward);

                gridModAPI.RemoveBlock(b, false);
                gridModAPI.AddBlock(obj, false);
            }

            replaceBlocks.Clear();
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ReflectorLight), false)]
    public class Spotlight : MyGameLogicComponent
    {
        IMyLightingBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyLightingBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            block.IsWorkingChanged += UpdateEmissivity;
            block.PropertiesChanged += UpdateEmissivity;
            UpdateEmissivity(block);
        }

        public override void Close()
        {
            block.IsWorkingChanged -= UpdateEmissivity;
            block.PropertiesChanged -= UpdateEmissivity;
        }

        private void UpdateEmissivity(IMyCubeBlock notUsed)
        {
            if(Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, block.WorldMatrix.Translation) > 5000 * 5000)
                return;

            float intensity = 0f;

            if(block.IsWorking)
            {
                var def = (MyLightingBlockDefinition)block.SlimBlock.BlockDefinition;

                intensity = 0.5f + ((block.Intensity - def.LightIntensity.Min) / (def.LightIntensity.Max - def.LightIntensity.Min)) * 0.5f;
            }

            block.SetEmissiveParts("Bulb", block.Color, intensity);
            block.SetEmissiveParts("Reflector", (block.IsWorking ? block.Color : Color.White), intensity);

            // "fix" for lighting blocks not reacting instantly to on/off toggle
            block.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorLight), false)]
    public class InteriorLight : MyGameLogicComponent
    {
        IMyLightingBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyLightingBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            block.IsWorkingChanged += IsWorkingChanged;
            IsWorkingChanged(block);
        }

        public override void Close()
        {
            block.IsWorkingChanged -= IsWorkingChanged;
        }

        private void IsWorkingChanged(IMyCubeBlock notUsed)
        {
            if(Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, block.WorldMatrix.Translation) > 5000 * 5000)
                return;

            // "fix" for lighting blocks not reacting instantly to on/off toggle
            block.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }
    }
}