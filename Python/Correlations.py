# -*- coding: utf-8 -*-
"""
Created on Wed Aug 28 09:46:05 2019

@author: Schimpf
"""

import os
import numpy as np
from matplotlib import pyplot as plt

path = r'E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\G2_X.txt'

files = []
# r=root, d=directories, f = files
for r, d, f in os.walk(path):
    for file in f:
        if '.txt' in file:
            files.append(os.path.join(r, file))

fig=plt.figure(figsize=(10,20))

index = 0
for file in [files[i] for i in [2,3,6,7]]:
    data =  np.loadtxt(file, dtype=np.float64, delimiter=',')
    ax=fig.add_subplot(2,2,index+1)
    
    ax.plot(data[:,0]/1E3,data[:,1])
    ax.set_title(file[-6:-4], fontsize=20)
    ax.set_xlabel('time delay [ns]')
    
    index = index+1
    
fig.tight_layout()
fig.show()



