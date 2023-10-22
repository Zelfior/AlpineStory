#!/usr/bin/env python3

import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates

"""

    This script takes a height map, and rescale it locally to reach the minimum and maximum in all the regions.
    It is useful for mountains like sceneries where the average altitude is not constant on the map.

"""
data = Image.open("HeightMap.png")

cut_size = 2

image = np.array(data)[:,:,0]

init_resolution_x = image.shape[0]
init_resolution_y = image.shape[0]

min_val = np.min(image)
max_val = np.max(image)
image = (image - min_val) / (max_val - min_val)*255

local_max = np.zeros([2,2])
local_min = np.zeros([2,2])

data = image

for i in range(2):
    for j in range(2):
        local_min[i,j] = np.min(data[init_resolution_x*i/cut_size:init_resolution_x/cut_size*(i+1), init_resolution_y/cut_size*j:init_resolution_y/cut_size*(j+1)])
        local_max[i,j] = np.max(data[init_resolution_x*i/cut_size:init_resolution_x/cut_size*(i+1), init_resolution_y/cut_size*j:init_resolution_y/cut_size*(j+1)])

new_dims = []
for original_length, new_length in zip(local_min.shape, (init_resolution_x, init_resolution_y)):
    new_dims.append(np.linspace(0, original_length-1, new_length))

coords = np.meshgrid(*new_dims, indexing='ij')
extended_local_min = map_coordinates(local_min, coords)

new_dims = []
for original_length, new_length in zip(local_max.shape, (init_resolution_x, init_resolution_y)):
    new_dims.append(np.linspace(0, original_length-1, new_length))

coords = np.meshgrid(*new_dims, indexing='ij')
extended_local_max = map_coordinates(local_max, coords)

data = (data-extended_local_min)/(extended_local_max-extended_local_min) 

im = Image.fromarray(data*255)
im = im.convert('RGB')
im.save("HeightMap_rescaled.png")