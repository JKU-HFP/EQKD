# -*- coding: utf-8 -*-
"""
Created on Wed Nov  6 12:14:12 2019

@author: Schimpf
"""

import numpy as np
from matplotlib import pyplot as plt

def ErrorCorrelation(aliceKey, bobKey, N=20):
    
    errors = np.full_like(aliceKey,0)
    """Get error array"""
    for i in range(len(aliceKey)):
        if(aliceKey[i]!=bobKey[i]):
            errors[i]=1
          

    histogram = np.zeros((N,))
    
    for i in range(len(errors)):
        if(errors[i]==1):
            for dist in range(N):
                if(i+dist<len(errors)):
                    if(errors[i+dist]==1):
                        histogram[dist] = histogram[dist]+1
    
    return histogram

alicefile="..\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long_unbiased_parityCorr.txt"
bobfile="..\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long_unbiased_parityCorr.txt"      

aliceKey = np.loadtxt(alicefile)
bobKey = np.loadtxt(bobfile)
         
N=20

histogram=ErrorCorrelation(aliceKey,bobKey,N)

f = plt.figure(figsize=(5,3))
ax=f.add_subplot(111)

ax.bar(range(1,N),histogram[1:])
ax.set_xticks(range(1,N,2))

f.show()                