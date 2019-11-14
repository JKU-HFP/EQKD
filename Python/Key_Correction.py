# -*- coding: utf-8 -*-
"""
Created on Wed Nov 13 13:50:12 2019

@author: Schimpf

Correction of existing key obtained by QKD
"""


from matplotlib import pyplot as plt
import numpy as np
import time

import Key_Analysis_Lib as ka
import Key_Correction_Lib as kc



alicefile="..\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long.txt"
bobfile="..\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long.txt"

aliceKey = np.loadtxt(alicefile)
bobKey = np.loadtxt(bobfile)

print("----- 01. REMOVE BIAS ----------")

newA,newB = aliceKey,bobKey
bias_tolerance=1E-5
bias=ka.KeyProbDist(aliceKey)[1]
num_removals=0
start_time=time.time()

"""Remove bias until tolerance reached"""
while np.abs((bias-0.5))>bias_tolerance:
    bias=ka.KeyProbDist(newA)[1]
    newA,newB=kc.RemoveBias(newA,newB,bias)
    num_removals+=1
end_time=time.time()

print("Remaining bias: {}".format(bias))
print("Shannon entropy: {}".format(ka.SEntropy(ka.KeyProbDist(newA))))
print("Bias removal time: {}".format(end_time-start_time))
print("Number of bias removal cycles: {}".format(num_removals))
print("Efficiency: {}".format(len(newA)/len(aliceKey)))

aliceKey,bobKey = newA,newB

print("----------------------------")