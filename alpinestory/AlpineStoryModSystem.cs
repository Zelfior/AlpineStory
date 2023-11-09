using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using SkiaSharp;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

/**

    Notes on the world generation.

    The world generation is made in several passes. The passes are defined in the enum EnumWorldGenPass.
    
    Within a pass, the generation is done in layers are ordered based on the index given by the function ExecuteOrder(). 
    The loop in StartServerSide deletes all currently registered world gen path (therefore with an index lower than this class ExecuteOrder).

    The vanilla passes are the following:
        -      0      GenTerra : creates the vanilla landscape heights.
        -      0.1    GenRockStrata : layers the GenTerra height in different materials.
        -      0.12   GenDungeons : generates the underground dungeons.
        -      0.2    GenDeposits : generates the ore deposits.
        -      0.3    GenStructures : generates the world structures.
        -      0.4    GenBlockLayers : generates ice, grass...
        -      0.5    GenPonds : generates lakes...
        -      0.5    GenVegetationAndPatches : generates forests and plants.

    The source code of the vanilla world generation can be found in the VS survival source code:
    https://github.com/anegostudios/vssurvivalmod.git/Systems/WorldGen/Standard

    This mode replaces the layers GenTerra and GenRockStrata to create a map based on a .png image. 
    The generation is purposously split in several steps for better code readability:
        -      0      AlpineTerrain : Generates the granite main layer, and a layer of dirt on top of it (replaces GenTerra).
        -      1      AlpineStrata : Adds a layer of another stone below the surface using the vanilla noise generator (replaces GenRockStrata).
        -      2      AlpineFloor : parametrise the data that are used later to generate plants and the block cover.
        -      3      AlpineFloorCorrection : Correcting some of the vanilla world gen features that don't match the expected result (dirt layer too thick, dirt or snow on cliff side).
        -      4      AlpineRiver : Remove the plants from the rivers/lakes.

*/
namespace AlpineStoryMod
{
    public class AlpineStoryModSystem : ModStdWorldGen
    {
        ICoreServerAPI api;
        SKBitmap height_map;
        SKBitmap[] heightMaps;
        SKBitmap[] regionMaps;
        internal float data_width_per_pixel;
        internal int min_height_custom; 
        AlpineMapGenerator alpineMapGenerator;
        AlpineTerrain alpineTerrain;
        AlpineStrata alpineStrata;
        AlpineFloor alpineFloor;
        AlpineFloorCorrection alpineFloorCorrection;
        AlpineRiver alpineRiver;
        BiomeGrid biomeGrid;
        int[] lakeMap;
        UtilTool uTool;
        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        //  The ExecuteOrder here is important as it has to be after all worldgen objects we want to delete (see StartServerSide function).
        public override double ExecuteOrder()
        {
            return 0.2;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            //  Removing all generator that was registered so far
            foreach (int enumvalue in Enum.GetValues(typeof(EnumWorldGenPass)))
            {
                if (enumvalue < ((ServerMain)api.World).ModEventManager.WorldgenHandlers["standard"].OnChunkColumnGen.Length)
                {
                    if (enumvalue != (int)EnumWorldGenPass.NeighbourSunLightFlood && enumvalue != (int)EnumWorldGenPass.PreDone)
                    {
                        var handlers = ((ServerMain)api.World).ModEventManager.WorldgenHandlers["standard"].OnChunkColumnGen[enumvalue] ;
                        List<int> toRemove = new List<int>();

                        if(handlers != null)
                        {
                            //  Condition on which object type we want to remove
                            for (int i = 0; i < handlers.Count; i++){
                                var type = handlers[i].Method.DeclaringType;
                                if (type == typeof(GenTerra) || 
                                    type == typeof(GenRockStrataNew) || 
                                    type == typeof(GenTerraPostProcess)){
                                    toRemove.Add(i);
                                }
                            }
                            for (int i = toRemove.Count - 1; i >= 0 ; i--){
                                handlers.RemoveAt(toRemove[i]) ;
                            }
                        }
                    }
                }
            }

            //  In this mod, the X - Z coordinates are scaled based on the map Y size
            data_width_per_pixel = api.WorldManager.MapSizeY / 256;
            min_height_custom = api.World.SeaLevel;

            //  Change this boolean to True to generate a climate (temperature - rain) mapping, for debug/information purpose.
            bool generateBiomeGrid = false ;

            //  We give a 2048 - 2048 offset of the map to not start on the borders
            uTool = new UtilTool(api, 2048, 2048);

            if(generateBiomeGrid){
                biomeGrid = new BiomeGrid(api, height_map, data_width_per_pixel, min_height_custom);
                api.Event.ChunkColumnGeneration(biomeGrid.OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
            }
            else{
                //  Reading the height map that will be provided to all world generation passes
                int bitmaps_count = 20;
                heightMaps = new SKBitmap[bitmaps_count];
                regionMaps = new SKBitmap[bitmaps_count];

                for(int i = 0; i< bitmaps_count; i++){
                    IAsset asset = this.api.Assets.Get(new AssetLocation("alpinestory:worldgen/alpsalpha/alps_"+(i+1).ToString("00")+".png"));
                    BitmapExternal bmpt = new BitmapExternal(asset.Data, asset.Data.Length, api.Logger);

                    heightMaps[i] = bmpt.bmp;

                    //  The region maps correspond to macro maps of the world, (one pixel per chunk)
                    //      -   regionMap is used to set climates based on the local altitude
                    //      -   lakeMap is used to forbid forest in lakes
                    
                    // pixels[dz * wdt + dx] = ColorUtil.ColorFromRgba(precipi, precipi, precipi, 255);
                    int[] averageHeights = uTool.build_region_map(heightMaps[i], api.WorldManager.ChunkSize, data_width_per_pixel, min_height_custom, api.WorldManager.MapSizeY, 0);
                    // Array.Reverse(averageHeights);
                    regionMaps[i] = new SKBitmap(heightMaps[i].Width/api.WorldManager.ChunkSize, heightMaps[i].Width/api.WorldManager.ChunkSize);
                    
                    for(int k = 0; k < heightMaps[i].Width/api.WorldManager.ChunkSize; k++){
                        for(int l = 0; l < heightMaps[i].Width/api.WorldManager.ChunkSize; l++){
                            int index = uTool.ChunkIndex2d(k, l, heightMaps[i].Width/api.WorldManager.ChunkSize);
                            int localValue = Math.Clamp(averageHeights[index], 0, 255);
                            regionMaps[i].SetPixel(k, l, new SKColor((byte)localValue, (byte)localValue, (byte)localValue));
                        }
                    }
                    // regionMaps[i].Save("extracted_map_"+(i+1).ToString()+".png");
                }


                //  Int random generator used as criterion to spawn halite
                Random rand = new Random();

                //  Initialize temperature bias for starting climate
                
                ITreeAttribute worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
                int temperature_bias = 0;
                string startingClimate = worldConfig.GetString("startingClimate");
                switch (startingClimate)
                {
                    case "hot":
                        temperature_bias = TerraGenConfig.DescaleTemperature(30) - TerraGenConfig.DescaleTemperature(10);
                        break;
                    case "warm":
                        temperature_bias = TerraGenConfig.DescaleTemperature(20) - TerraGenConfig.DescaleTemperature(10);
                        break;
                    case "cool":
                        temperature_bias = TerraGenConfig.DescaleTemperature(0) - TerraGenConfig.DescaleTemperature(10);
                        break;
                    case "icy":
                        temperature_bias = TerraGenConfig.DescaleTemperature(-10) - TerraGenConfig.DescaleTemperature(10);
                        break;
                }

                //  Creating an instance of each generation function
                alpineMapGenerator = new AlpineMapGenerator(api, heightMaps, data_width_per_pixel, min_height_custom, uTool);
                alpineTerrain = new AlpineTerrain(api, heightMaps, data_width_per_pixel, min_height_custom, uTool);
                alpineStrata = new AlpineStrata(api, data_width_per_pixel, min_height_custom, uTool);
                alpineFloor = new AlpineFloor(api, data_width_per_pixel, min_height_custom, temperature_bias, regionMaps, uTool);
                alpineFloorCorrection = new AlpineFloorCorrection(api, height_map, data_width_per_pixel, min_height_custom, rand, uTool);
                alpineRiver = new AlpineRiver(api, height_map, data_width_per_pixel, min_height_custom, uTool);

                //  Registering the generation function in the Terrain pass. It is not necessary to have them stored in different files.
                api.Event.ChunkColumnGeneration(alpineMapGenerator.OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
                api.Event.ChunkColumnGeneration(alpineTerrain.OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
                api.Event.ChunkColumnGeneration(alpineStrata.OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
                api.Event.ChunkColumnGeneration(alpineFloor.OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
                api.Event.ChunkColumnGeneration(alpineFloorCorrection.OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.ChunkColumnGeneration(alpineRiver.OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
            }

            //  Don't you dare removing that line, it would silently break some pass of the vanilla world gen.
            api.Event.InitWorldGenerator(initWorldGen, "standard");
        }
        
        public void initWorldGen()
        {
            LoadGlobalConfig(api);
        }
    }

    internal class BlockAccessor
    {
    }

}