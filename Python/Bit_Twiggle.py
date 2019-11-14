# -*- coding: utf-8 -*-
"""
Created on Tue Nov 12 12:25:14 2019

@author: Schimpf
"""

"""Try to twiggle"""

import numpy as np
import scipy as sc
import hashlib
import time
from matplotlib import pyplot as plt
from matplotlib import image as im

def HashKey(k):
    hashA = hashlib.new('ripemd160')
    hashA.update(k)
    return hashA.hexdigest()

def Invert(val):
    if(val == 0): return 1
    if(val == 1): return 0

def Twiggle(keyA,keyB,packetsize):

    starttime=time.time()
    
    offs=0
    
    aNew = np.array(0)
    bNew = np.array(0)
    
    while(offs+packetsize < len(keyA)):
        
        packA=keyA[offs:offs+packetsize]  
        packB=keyB[offs:offs+packetsize]
        
        """Packet already correct?"""
        if(HashKey(packA)==HashKey(packB)):
                aNew = np.append(aNew,packA)
                bNew = np.append(bNew,packB)
        else:           
            for i in range(len(packA)):
                """Invert bit"""
                packA[i] = Invert(packA[i])
                if(HashKey(packA)==HashKey(packB)):
                    aNew = np.append(aNew,packA)
                    bNew = np.append(bNew,packB)
                    break
                else:
                    packA[i] = Invert(packA[i])
                
        offs += packetsize
     
    timespan = time.time() - starttime
   
    return {"keyA": aNew,
            "keyB": bNew,
            "efficiency": len(aNew)/len(keyA),
            "timespan": timespan}
    

psizes = [100,200,300,400]
timespans=[]
efficiencies=[]

for size in psizes:   
    res=Twiggle(aliceKeyNew,bobKeyNew,size)
    timespans.append(res["timespan"])
    efficiencies.append(res["efficiency"]*100)

f = plt.figure(figsize=(10,5))

timesAx = f.add_subplot(121)
timesAx.plot(psizes,timespans)
timesAx.set_xlabel('Packet size')
timesAx.set_ylabel('Calc. time (s)')

effAx = f.add_subplot(122)
#effAx.plot(psizes,efficiencies)
#effAx.set_xlabel('Packet size')
#effAx.set_ylabel('Efficiency (%)')
bmp=im.imread("Decr.bmp")
effAx.imshow(bmp)

f.show()


twiggle=Twiggle(aliceKeyNew,bobKeyNew,100)
