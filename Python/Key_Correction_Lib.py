# -*- coding: utf-8 -*-
"""
Created on Wed Oct 16 11:40:35 2019

@author: Schimpf

Methods for correcting keys from QKD

"""


import numpy as np
import hashlib
import time

def RemoveBias(keyA,keyB,bias):
    """Remove bias from given key"""
    
    probability = np.abs(bias-0.5)*2
    
    if(bias>0.5): key_to_cut=1
    else: key_to_cut=0
    
    newKeyA=[]
    newKeyB=[]
    for kA,kB in zip(keyA,keyB):
        
        if(kA==key_to_cut and np.random.random()<probability): continue
    
        newKeyA.append(kA)
        newKeyB.append(kB)
    
    return newKeyA,newKeyB

def ParityCorrect(aliceKey, bobKey, n):   
    """
    - Slices alice and bobs key into junks of size n
    - Removes 1 bit per junk to earse information contained by the parity
    - Omits packets with non-matching parity
    """
    cropA= (len(aliceKey)%n)
    cropB= (len(bobKey)%n)
    
    if(cropA != 0):
        aliceKeyRed = aliceKey[0:-cropA] #crop last subpacket
    else:
        aliceKeyRed=aliceKey
    ak = aliceKeyRed.reshape( (len(aliceKeyRed)//n),n ) 
    
    if(cropB !=0):
        bobKeyRed = bobKey[0:-cropB] #crop last subpacket
    else:
        bobKeyRed=bobKey
    bk = bobKeyRed.reshape( (len(bobKeyRed)//n), n )
    
    parity = lambda b: int(b.sum())%2
    
    aliceKeyNew = []
    bobKeyNew = []
    
    for i in range(len(ak)):
        if(parity(ak[i,:])==parity(bk[i,:])):
            aliceKeyNew.append(ak[i,:-1])
            bobKeyNew.append(bk[i,:-1])
        
    aliceKeyNew = np.ravel(aliceKeyNew).astype(int);        
    bobKeyNew = np.ravel(bobKeyNew).astype(int); 
    
    return aliceKeyNew, bobKeyNew    


def ShuffleKeys(kA,kB):
    """Shuffles keys by a random permutation"""
    if(len(kA)!=len(kB)):
        raise Exception('Keys not of equal length')
    
    t=np.array([kA,kB]).T
    np.random.shuffle(t)
    kAnew=t[:,0]
    kBnew=t[:,1]
    
    return kAnew,kBnew

def HashKey(k):
    """Calculates hash of given key"""
    hashA = hashlib.new('ripemd160')
    hashA.update(k)
    return hashA.hexdigest()

def Invert(val):
    """Inverts keybit"""
    if(val == 0): return 1
    if(val == 1): return 0

def Twiggle(keyA,keyB,packetsize):
    """Error eleminiation by bit twiggling of consecutive packets"""

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







