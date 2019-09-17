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
using VRage.Utils;
using VRageMath;

namespace Digi.LightBlockImprovements
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class LightBlockImprovementsMod : MySessionComponentBase
    {
        private readonly MyDefinitionId OLD_LIGHT_DEFID = new MyDefinitionId(typeof(MyObjectBuilder_LightingBlock), MyStringHash.GetOrCompute("SmallLight2"));
        private const string NEW_LIGHT_SUBID = "SmallBlockSmallLight"; // subtype ID to replace the above block with

        private readonly HashSet<IMySlimBlock> replaceBlocks = new HashSet<IMySlimBlock>();

        public override void LoadData()
        {
            if(MyAPIGateway.Session.IsServer)
            {
                MyEntities.OnEntityAdd += OnEntityAdded;
            }

            // Dynamic definition editing without needing to set the other values
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

            def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_1corner", @"Models\LargeCornerLight.mwm");
            def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_1corner", @"Models\SmallCornerLight.mwm");

            def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_2corner", @"Models\LargeCornerLightDouble.mwm");
            def = GetDefAndSetModel(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_2corner", @"Models\SmallCornerLightDouble.mwm");
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

        // Used for replacing a block in every grid that spawns; must be only server side
        private void OnEntityAdded(MyEntity ent)
        {
            try
            {
                var grid = ent as MyCubeGrid;

                if(grid == null)
                    return;

                replaceBlocks.Clear();

                var blocks = grid.GetBlocks();
                var gridModAPI = (IMyCubeGrid)ent;

                foreach(IMySlimBlock b in blocks)
                {
                    if(b.BlockDefinition.Id == OLD_LIGHT_DEFID)
                    {
                        replaceBlocks.Add(b);
                    }
                }

                foreach(var b in replaceBlocks)
                {
                    var obj = b.GetObjectBuilder(false);
                    obj.SubtypeName = NEW_LIGHT_SUBID;
                    obj.BlockOrientation = new MyBlockOrientation(Base6Directions.GetOppositeDirection(obj.BlockOrientation.Up), obj.BlockOrientation.Forward); // initial block rotation is different, needs correcting

                    gridModAPI.RemoveBlock(b, false);
                    gridModAPI.AddBlock(obj, false);
                }

                replaceBlocks.Clear();
            }
            catch(Exception e)
            {
                MyLog.Default.WriteLine(e);
                MyAPIGateway.Utilities.ShowNotification($"[ Error in {GetType().FullName}: {e.Message} ]", 10000, MyFontEnum.Red);
            }
        }
    }
}