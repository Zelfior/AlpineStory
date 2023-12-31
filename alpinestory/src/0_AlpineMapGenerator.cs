using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using System.Linq;
using System.Linq.Expressions;

public class AlpineMapGenerator: ModStdWorldGen
{
    ICoreServerAPI api;
    internal SKBitmap[] height_maps;
    internal float data_width_per_pixel;        
    internal int max_height_custom;
    internal int min_height_custom; 
    internal UtilTool uTool;
    public AlpineMapGenerator(){}
    public AlpineMapGenerator(ICoreServerAPI api, SKBitmap[] height_map, float data_width_per_pixel, int min_height_custom, UtilTool uTool)
    {
        LoadGlobalConfig(api);
        
        this.api = api;
        this.height_maps = height_map;

        //  The ColumnResult object will contain the data of the chunks to generate
        columnResults = new ColumnResult[chunksize * chunksize];
        for (int i = 0; i < chunksize * chunksize; i++) columnResults[i].ColumnBlockSolidities = new BitArray(api.WorldManager.MapSizeY);

        max_height_custom = api.WorldManager.MapSizeY;
        this.data_width_per_pixel = data_width_per_pixel;
        this.min_height_custom = min_height_custom;

        // Tools dedicated to this mod mainly to interpolate between pixels and pre-process the heightmap
        this.uTool = uTool;
    }
    public override double ExecuteOrder()
    {
        return 0;
    }
    ColumnResult[] columnResults;
    public void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {   
        generate(request.Chunks, request.ChunkX, request.ChunkZ, request.RequiresChunkBorderSmoothing);
    }
    
    private int riverWidth(int defaultWidth, int altitude){
        return (int)((float)defaultWidth * (float)(max_height_custom - altitude) / (float)(max_height_custom - min_height_custom));
    }
    private void generate(IServerChunk[] chunks, int chunkX, int chunkZ, bool requiresChunkBorderSmoothing)
    {
        int interMountainChunkCount = 15;
        int maxRiverRadius = 4;

        //  Storing here the results for each X - Z coordinates (Y being the vertical) of the map pre-processing
        int[] marginedHeightMap;
        int[] localRiverRadius = new int[(chunksize + 2*maxRiverRadius)*(chunksize + 2*maxRiverRadius)];
        int[] elementMap ;

        MapElementManager MEM = new MapElementManager(api, uTool, chunkX, chunkZ, min_height_custom, max_height_custom, height_maps);
        MapElement[] elements = MEM.getLocalMapElements(interMountainChunkCount, chunkX, chunkZ);

        (marginedHeightMap, elementMap) = MEM.generateHeightMap(elements, interMountainChunkCount, chunkX, chunkZ, maxRiverRadius);

        //  Setting the chunk height map.
        int[] chunkHeightMap = new int[chunksize*chunksize];
        for(int lX=0; lX < chunksize; lX++){
            for(int lZ=0; lZ < chunksize; lZ++){
                chunkHeightMap[uTool.ChunkIndex2d(lX, lZ, chunksize)] = marginedHeightMap[uTool.ChunkIndex2d(lX + maxRiverRadius, lZ + maxRiverRadius, chunksize + 2*maxRiverRadius)];
            }
        }

        //  Detecting the zone edges
        for(int lX=0; lX < chunksize + 2*maxRiverRadius; lX++){
            for(int lZ=0; lZ < chunksize + 2*maxRiverRadius; lZ++){
                int[] neighbours = new int[4];

                if (lX - 1 >= 0){
                    neighbours[0] = elementMap[uTool.ChunkIndex2d(lX-1, lZ, chunksize + 2*maxRiverRadius)];
                }
                
                if (lZ + 1 < chunksize + 2*maxRiverRadius){
                    neighbours[1] = elementMap[uTool.ChunkIndex2d(lX, lZ+1, chunksize + 2*maxRiverRadius)];
                }

                if (lX + 1 < chunksize + 2*maxRiverRadius){
                    neighbours[2] = elementMap[uTool.ChunkIndex2d(lX+1, lZ, chunksize + 2*maxRiverRadius)];
                }

                if (lZ - 1 >= 0){
                    neighbours[3] = elementMap[uTool.ChunkIndex2d(lX, lZ-1, chunksize + 2*maxRiverRadius)];
                }

                for(int i=0; i<4; i++){
                    if(neighbours[i] == 0) neighbours[i] = elementMap[uTool.ChunkIndex2d(lX, lZ, chunksize + 2*maxRiverRadius)];
                }

                if (neighbours.Max() != neighbours.Min()){
                    Random r = new Random(neighbours.Min() + neighbours.Max());
                    localRiverRadius[uTool.ChunkIndex2d(lX, lZ, chunksize + 2*maxRiverRadius)] = r.Next(0, 4);

                    if (localRiverRadius[uTool.ChunkIndex2d(lX, lZ, chunksize + 2*maxRiverRadius)] == 1)
                        localRiverRadius[uTool.ChunkIndex2d(lX, lZ, chunksize + 2*maxRiverRadius)] = 0;
                }
            }
        }

        //  Loop on each lX, lZ values
        bool toBreak;
        int[] riverMap = new int[(chunksize + 2*maxRiverRadius)*(chunksize + 2*maxRiverRadius)];
        // int[] chunkRadiusMap = new int[chunksize*chunksize];

        for(int lX=0; lX < chunksize + 2*maxRiverRadius; lX++){
            for(int lZ=0; lZ < chunksize + 2*maxRiverRadius; lZ++){
                toBreak = false;
                for(int x=-maxRiverRadius; x < maxRiverRadius+1; x++){
                    int zval = (int)Math.Sqrt(maxRiverRadius*maxRiverRadius - x*x);

                    for(int z=-zval; z < zval+1; z++){
                        int colId = uTool.ChunkIndex2d(lX + x, lZ + z, chunksize + 2*maxRiverRadius);

                        if (lX + x >= 0 && lX + x < chunksize + 2*maxRiverRadius && lZ + z >= 0 && lZ + z < chunksize + 2*maxRiverRadius){
                            if (localRiverRadius[colId] > 0
                                    && Math.Sqrt(x*x + z*z) <= riverWidth(localRiverRadius[colId], marginedHeightMap[colId])){
                                riverMap[uTool.ChunkIndex2d(lX, lZ, chunksize + 2*maxRiverRadius)] = 1;
                                // chunkRadiusMap[uTool.ChunkIndex2d(lX, lZ, chunksize)] = riverWidth(chunkElementBorders[colId], marginedHeightMap[colId]);
                                toBreak = true;
                                break;
                            }
                        }
                    }

                    if (toBreak){
                        break;
                    }
                }
            }
        }

        //  We find here all 2 high gap to increase the height there, it can prevent having 2 blocks wide steps, but is not necessary
        int[] to_increase = uTool.analyse_chunk(chunkHeightMap, chunkX, chunkZ, chunksize, min_height_custom, max_height_custom, data_width_per_pixel, 0);

        for (int lZ = 0; lZ < chunksize*chunksize; lZ++){
            if (to_increase[lZ] == 1){
                chunkHeightMap[lZ] += 1;
            }
        }

        int[] riverHeightMap = new int[(chunksize + 2*maxRiverRadius)*(chunksize + 2*maxRiverRadius)];
        for (int colId = 0; colId < (chunksize + 2*maxRiverRadius)*(chunksize + 2*maxRiverRadius); colId++){
            int lX = colId% (chunksize + 2*maxRiverRadius);
            int lZ = colId/ (chunksize + 2*maxRiverRadius);

            if(riverMap[colId] == 1){
                riverHeightMap[colId] = uTool.getRiverHeight(lX, lZ, chunksize + 2*maxRiverRadius, marginedHeightMap, riverMap, maxRiverRadius);

                if (riverHeightMap[colId] == 1000)
                    riverHeightMap[colId] = marginedHeightMap[colId];
            }
        }
        
        int[] chunkRiverHeightMap = new int[chunksize*chunksize];
        int localValue, count;
        for(int lX=0; lX < chunksize; lX++){
            for(int lZ=0; lZ < chunksize; lZ++){
                localValue = 0;
                count = 0;
                for(int x=-maxRiverRadius; x < maxRiverRadius+1; x++){
                    for(int z=-maxRiverRadius; z < maxRiverRadius+1; z++){
                        int colId = uTool.ChunkIndex2d(lX + x + maxRiverRadius, lZ + z + maxRiverRadius, chunksize + 2*maxRiverRadius);
                        if (lX + x + maxRiverRadius>= 0 && 
                                lX + x + maxRiverRadius < chunksize + 2*maxRiverRadius && 
                                lZ + z + maxRiverRadius >= 0 && 
                                lZ + z + maxRiverRadius < chunksize + 2*maxRiverRadius && 
                                riverMap[colId] == 1){
                            localValue += riverHeightMap[colId];
                            count += 1;
                        }
                    }
                }
                chunkRiverHeightMap[uTool.ChunkIndex2d(lX, lZ, chunksize)] = (int) ((float)localValue/count);
            }
        }

        int[] chunkRiverMap = new int[chunksize*chunksize];
        //  Setting the chunk height map.
        for(int lX=0; lX < chunksize; lX++){
            for(int lZ=0; lZ < chunksize; lZ++){
                chunkRiverMap[uTool.ChunkIndex2d(lX, lZ, chunksize)] = riverMap[uTool.ChunkIndex2d(lX + maxRiverRadius, lZ + maxRiverRadius, chunksize + 2*maxRiverRadius)];
            }
        }

        /*
            Saving the height map for future uses
        */
        chunks[0].MapChunk.MapRegion.SetModdata("Alpine_HeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString(), chunkHeightMap);
        chunks[0].MapChunk.MapRegion.SetModdata("Alpine_RiverMap_"+chunkX.ToString()+"_"+chunkZ.ToString(), chunkRiverMap);
        chunks[0].MapChunk.MapRegion.SetModdata("Alpine_RiverHeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString(), chunkRiverHeightMap);
    }
}
