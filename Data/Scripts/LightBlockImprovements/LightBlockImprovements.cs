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
using VRage.ObjectBuilders;
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
        private readonly Dictionary<MyDefinitionId, BackupDefData> defDataBackups = new Dictionary<MyDefinitionId, BackupDefData>(MyDefinitionId.Comparer);

        public override void LoadData()
        {
            try
            {
                Log.ModName = "Light Block Improvements";
                Log.AutoClose = false;

                if(MyAPIGateway.Session.IsServer)
                {
                    MyEntities.OnEntityAdd += OnEntityAdded;
                }

                EditDefinitions();
            }
            catch(Exception e)
            {
                Log.Error(e);
                UnloadData();
                throw;
            }
        }

        protected override void UnloadData()
        {
            try
            {
                if(MyAPIGateway.Session.IsServer)
                {
                    MyEntities.OnEntityAdd -= OnEntityAdded;
                }

                RestoreVanillaDefinitions();
            }
            finally
            {
                Log.Close();
            }
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

        #region Definition editing
        private void EditDefinitions()
        {
            SimpleModify(typeof(MyObjectBuilder_InteriorLight), "SmallLight", @"Models\LargeInteriorLight.mwm", falloff_min: 0.5f, intensity_min: 0.5f);
            SimpleModify(typeof(MyObjectBuilder_InteriorLight), "SmallBlockSmallLight", @"Models\SmallInteriorLight.mwm", falloff_min: 0.5f, intensity_min: 0.5f);
            SimpleModify(typeof(MyObjectBuilder_ReflectorLight), "LargeBlockFrontLight", @"Models\LargeSpotlight.mwm");
            SimpleModify(typeof(MyObjectBuilder_ReflectorLight), "SmallBlockFrontLight", @"Models\SmallSpotlight.mwm");
            SimpleModify(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_1corner", @"Models\LargeCornerLight.mwm");
            SimpleModify(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_1corner", @"Models\SmallCornerLight.mwm");
            SimpleModify(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_2corner", @"Models\LargeCornerLightDouble.mwm");
            SimpleModify(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_2corner", @"Models\SmallCornerLightDouble.mwm");
        }

        private void SimpleModify(MyObjectBuilderType obType, string subType, string modelRelativePath, float? falloff_min = null, float? intensity_min = null)
        {
            var lightDef = GetLightDefById(obType, subType);

            if(lightDef == null)
            {
                Log.Error($"Couldn't find def: {obType.ToString()}/{subType}");
                return;
            }

            var backup = new BackupDefData();

            if(modelRelativePath != null)
            {
                backup.Model = lightDef.Model;
                lightDef.Model = Path.Combine(ModContext.ModPath, modelRelativePath);
            }

            if(falloff_min.HasValue)
            {
                backup.LightFalloff = lightDef.LightFalloff; // MyBounds is struct, gets copied
                lightDef.LightFalloff.Min = falloff_min.Value;
            }

            lightDef.LightFalloff.Default = 1f;

            if(intensity_min.HasValue)
            {
                backup.LightIntensity = lightDef.LightIntensity;
                lightDef.LightIntensity.Min = intensity_min.Value;
            }

            AddDefBackup(lightDef.Id, backup);
        }

        private void RestoreVanillaDefinitions()
        {
            if(defDataBackups == null)
                return;

            foreach(var kv in defDataBackups)
            {
                var id = kv.Key;
                var lightDef = GetLightDefById(id.TypeId, id.SubtypeName);

                if(lightDef == null)
                    continue; // shouldn't be possible but at this point who cares

                var backup = kv.Value;

                if(backup.Model != null)
                    lightDef.Model = backup.Model;

                if(backup.LightFalloff.HasValue)
                    lightDef.LightFalloff = backup.LightFalloff.Value;

                if(backup.LightIntensity.HasValue)
                    lightDef.LightIntensity = backup.LightIntensity.Value;
            }

            defDataBackups.Clear();
        }

        private MyLightingBlockDefinition GetLightDefById(MyObjectBuilderType type, string subtypeId)
        {
            MyCubeBlockDefinition def;

            if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(type, MyStringHash.GetOrCompute(subtypeId)), out def))
                return (MyLightingBlockDefinition)def;

            return null;
        }

        private void AddDefBackup(MyDefinitionId defId, BackupDefData data)
        {
            defDataBackups.Add(defId, data);
        }

        private class BackupDefData
        {
            public string Model;
            public MyBounds? LightFalloff;
            public MyBounds? LightIntensity;
        }
        #endregion Definition editing
    }
}