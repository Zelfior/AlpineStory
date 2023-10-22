#!/usr/bin/env python3

import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates

"""

    This script takes a height map, and rescale it locally to reach the minimum and maximum in all the regions.
    It is useful for mountains like sceneries where the average altitude is not constant on the map.

"""
data = Image.open("HeightMap.png")

image = np.array(data)[:,:,0]

min_val = np.min(image)
max_val = np.max(image)
image = (image - min_val) / (max_val - min_val)*255

local_max = np.zeros([8,8])
local_min = np.zeros([8,8])

data = image

for i in range(8):
    for j in range(8):
        local_min[i,j] = np.min(data[1000*i:1000*(i+1), 1000*j:1000*(j+1)])
        local_max[i,j] = np.max(data[1000*i:1000*(i+1), 1000*j:1000*(j+1)])

new_dims = []
for original_length, new_length in zip(local_min.shape, (8000,8000)):
    new_dims.append(np.linspace(0, original_length-1, new_length))

coords = np.meshgrid(*new_dims, indexing='ij')
extended_local_min = map_coordinates(local_min, coords)

new_dims = []
for original_length, new_length in zip(local_max.shape, (8000,8000)):
    new_dims.append(np.linspace(0, original_length-1, new_length))

coords = np.meshgrid(*new_dims, indexing='ij')
extended_local_max = map_coordinates(local_max, coords)

data = (data-extended_local_min)/(extended_local_max-extended_local_min) 

im = Image.fromarray(data*255)
im = im.convert('RGB')
im.save("HeightMap_rescaled.png")