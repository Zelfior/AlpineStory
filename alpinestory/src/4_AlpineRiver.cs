using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using SkiaSharp;
using Vintagestory.API.Util;

public class AlpineRiver: ModStdWorldGen
{
    ICoreServerAPI api;
    internal SKBitmap height_map;
    internal float data_width_per_pixel;        
    internal int max_height_custom;    
    internal int min_height_custom; 
    internal UtilTool uTool;
    public AlpineRiver(){}
    public AlpineRiver(ICoreServerAPI api, SKBitmap height_map, float data_width_per_pixel, int min_height_custom, UtilTool uTool)
    {
        LoadGlobalConfig(api);
        
        this.api = api;
        this.height_map = height_map;
        
        this.min_height_custom = min_height_custom;
        this.max_height_custom = api.WorldManager.MapSizeY;

        this.data_width_per_pixel = data_width_per_pixel;

        this.uTool = uTool;
    }
    public void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {   
        generate(request.Chunks, request.ChunkX, request.ChunkZ, request.RequiresChunkBorderSmoothing);
    }

    public override double ExecuteOrder()
    {
        return 0.05;
    }
    private void generate(IServerChunk[] chunks, int chunkX, int chunkZ, bool requiresChunkBorderSmoothing)
    { 
        //  We reiterate the river and lake making here, to remove the plants generated underwater by the vanilla worldgen.
        int muddyGravelID = api.World.GetBlock(new AssetLocation("muddygravel")).Id ;        
        int waterID = api.World.GetBlock(new AssetLocation("water-still-7")).Id ;

        //  Clean river beds
        cleanRiverBed(chunks, chunkX, chunkZ, waterID, muddyGravelID);

        //  Clean river beds
        // uTool.makeLakes(chunks, chunkX, chunkZ, chunksize, waterID, muddyGravelID, min_height_custom, max_height_custom, data_width_per_pixel, height_map);
    }
    public void cleanRiverBed(IServerChunk[] chunks, int chunkX, int chunkZ, int waterID, int gravelID){
        int altitude;
        int localRiverHeight;
        
        int[] chunkHeightMap = SerializerUtil.Deserialize<int[]>(chunks[0].MapChunk.MapRegion.GetModdata("Alpine_HeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString()));
        int[] chunkRiverMap = SerializerUtil.Deserialize<int[]>(chunks[0].MapChunk.MapRegion.GetModdata("Alpine_RiverMap_"+chunkX.ToString()+"_"+chunkZ.ToString()));
        int[] chunkRiverHeightMap = SerializerUtil.Deserialize<int[]>(chunks[0].MapChunk.MapRegion.GetModdata("Alpine_RiverHeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString()));
        
        for (int colId = 0; colId < chunksize*chunksize; colId++){
            int lX = colId% chunksize;
            int lZ = colId/ chunksize;

            if(chunkRiverMap[colId] == 1){
                altitude = chunkHeightMap[colId];

                localRiverHeight = chunkRiverHeightMap[colId];

                //  Checking if we are not removing a tree
                if(uTool.getBlockId(colId%chunksize, altitude-3, colId/chunksize, chunksize, chunks) == 0 ||
                    (uTool.getBlockId(colId%chunksize, altitude-3, colId/chunksize, chunksize, chunks) != 
                        uTool.getBlockId(colId%chunksize, altitude+1, colId/chunksize, chunksize, chunks))){

                    for(int posY = altitude-2; posY < Math.Min(localRiverHeight, max_height_custom); posY++){
                        uTool.SetBlockAir(colId%chunksize, posY, colId/chunksize, chunksize, chunks);
                        uTool.setBlockId(colId%chunksize, posY, colId/chunksize, chunksize, chunks, waterID, fluid:true);
                    }

                    if (altitude-2 == localRiverHeight){
                        uTool.SetBlockAir(colId%chunksize, localRiverHeight, colId/chunksize, chunksize, chunks);
                        uTool.setBlockId(colId%chunksize, localRiverHeight, colId/chunksize, chunksize, chunks, waterID, fluid:true);
                    }

                    for(int posY = localRiverHeight; posY < Math.Min(localRiverHeight+3, max_height_custom); posY++){
                        uTool.SetBlockAir(colId%chunksize, posY, colId/chunksize, chunksize, chunks);
                    }
                }

                uTool.setBlockId(colId%chunksize, altitude-4, colId/chunksize, chunksize, chunks, gravelID);
                uTool.setBlockId(colId%chunksize, altitude-3, colId/chunksize, chunksize, chunks, gravelID);
            }
        }
    }    
}