# -*- coding: utf-8 -*-
"""
Created on Wed Nov 13 13:50:12 2019

@author: Schimpf

Correction of existing key obtained by QKD
"""


from matplotlib import pyplot as plt
import numpy as np
import scipy as sc
import time

import Key_Analysis_Lib as ka
import Key_Correction_Lib as kc
import Bitmap_encoding as bmp


alicefile="keys/SecureKey_Alice_2Tagger_long.txt"
bobfile="keys/SecureKey_Bob_2Tagger_long.txt"

aliceKey = np.loadtxt(alicefile)
bobKey = np.loadtxt(bobfile)

print("----- 01. REMOVE BIAS ----------")

aliceKey,bobKey=kc.RemoveBias(aliceKey,bobKey)

opt_bias=ka.KeyProbDist(aliceKey)[1]
opt_entropy=ka.SEntropy(ka.KeyProbDist(aliceKey))

print("Remaining bias: {}".format(opt_bias))
print("Shannon entropy: {}".format(opt_entropy))

print("----- 02. First Parity Correction ----------")

QBER=ka.Qber(aliceKey,bobKey)

nran=[n for n in range(2,20)]

etaSim=list(map(lambda n: ka.eta(n,QBER),nran))
qberSim=ka.QCorr(nran,QBER)

etaReal = []
qberReal = []

aliceKey,bobKey=kc.PermuteKeys(aliceKey,bobKey,sc.random.permutation(len(aliceKey))) #Shuffle keys to eliminate correlations

for n in nran: 
    aliceKeyNew, bobKeyNew = kc.ParityCorrect(aliceKey,bobKey,n)
    e, qber = ka.KeyQuality(aliceKey,aliceKeyNew,bobKeyNew)
    etaReal.append(e)
    qberReal.append(qber)

f = plt.figure(figsize=(10,5))
f.suptitle("Original QBER: {:.2f}".format(QBER*100))

ax1=f.add_subplot(121)

ax1.plot(nran,etaSim,label='Model')
ax1.plot(nran,etaReal,ls='',marker="o",label='Measured key')
ax1.set_xlabel('n')
ax1.set_ylabel('Key efficiency')
ax1.set_xticks(nran)
ax1.legend()
ax1.grid(True)

ax2=f.add_subplot(122)
ax2.plot(nran,qberSim*100,label='Model')
ax2.plot(nran,list(map(lambda x: x*100.0,qberReal)),ls='',marker="o",label='Measured key')
ax2.set_xlabel('n')
ax2.set_ylabel('resulting QBER (%)')
ax2.set_xticks(nran)
ax2.legend()
ax2.grid(True)

f.tight_layout()
f.show()

"""Take n at maximum efficiency and parity correct"""
nopt =  nran[np.array(etaReal).argmax()]
print("Ideal blocksize: {}".format(nopt))

aliceKey,bobKey = kc.PermuteKeys(aliceKey,bobKey,sc.random.permutation(len(aliceKey))) #Shuffle keys to eliminate correlations
aliceKeyNew, bobKeyNew = kc.ParityCorrect(aliceKey,bobKey,nopt)
print("Key ratio: {}".format(len(aliceKeyNew)/len(aliceKey)))
print("Original QBER: {:.3f}%".format(100*ka.Qber(aliceKey,bobKey)))
print("New QBER: {:.3f}%".format(100*ka.Qber(aliceKeyNew,bobKeyNew)))

aliceKey,bobKey = aliceKeyNew,bobKeyNew


print("----- 03. Second Parity Correction ----------")

QBER=ka.Qber(aliceKey,bobKey)

nran=[n for n in range(2,40)]

etaSim=list(map(lambda n: ka.eta(n,QBER),nran))
qberSim=ka.QCorr(nran,QBER)

etaReal = []
qberReal = []

aliceKey,bobKey=kc.PermuteKeys(aliceKey,bobKey,sc.random.permutation(len(aliceKey))) #Shuffle keys to eliminate correlations

for n in nran: 
    aliceKeyNew, bobKeyNew = kc.ParityCorrect(aliceKey,bobKey,n)
    e, qber = ka.KeyQuality(aliceKey,aliceKeyNew,bobKeyNew)
    etaReal.append(e)
    qberReal.append(qber)

f = plt.figure(figsize=(10,5))
f.suptitle("Original QBER: {:.2f}".format(QBER*100))

ax1=f.add_subplot(121)

ax1.plot(nran,etaSim,label='Model')
ax1.plot(nran,etaReal,ls='',marker="o",label='Measured key')
ax1.set_xlabel('n')
ax1.set_ylabel('Key efficiency')
#ax1.set_xticks(nran)
ax1.legend()
ax1.grid(True)

ax2=f.add_subplot(122)
ax2.plot(nran,qberSim*100,label='Model')
ax2.plot(nran,list(map(lambda x: x*100.0,qberReal)),ls='',marker="o",label='Measured key')
ax2.set_xlabel('n')
ax2.set_ylabel('resulting QBER (%)')
#ax2.set_xticks(nran[::4])
ax2.legend()
ax2.grid(True)

f.tight_layout()
f.show()

"""Take n at maximum efficiency and parity correct"""
nopt =  nran[np.array(etaReal).argmax()]
print("Ideal blocksize: {}".format(nopt))

aliceKey,bobKey = kc.PermuteKeys(aliceKey,bobKey,sc.random.permutation(len(aliceKey))) #Shuffle keys to eliminate correlations
aliceKeyNew, bobKeyNew = kc.ParityCorrect(aliceKey,bobKey,nopt)
print("Key ratio: {}".format(len(aliceKeyNew)/len(aliceKey)))
print("Original QBER: {:.3f}%".format(100*ka.Qber(aliceKey,bobKey)))
print("New QBER: {:.3f}%".format(100*ka.Qber(aliceKeyNew,bobKeyNew)))

aliceKey,bobKey = aliceKeyNew,bobKeyNew

print("----- 04. Bit twiggling ----------")

aliceKey,bobKey=kc.PermuteKeys(aliceKey,bobKey,sc.random.permutation(len(aliceKey))) #Shuffle keys to eliminate correlations

twiggleResult = kc.Twiggle(aliceKey,bobKey,1000)
print("Processing time: {}".format(twiggleResult["timespan"]))
print("Key ratio: {}".format(twiggleResult["efficiency"]))
print("Original QBER: {:.3f}%".format(100*ka.Qber(aliceKey,bobKey)))
print("New QBER: {:.3f}%".format(100*ka.Qber(twiggleResult["keyA"],twiggleResult["keyB"])))

aliceKey,bobKey=twiggleResult["keyA"],twiggleResult["keyB"]

print("----- 05. REMOVE BIAS ----------")

newA,newB = aliceKey,bobKey
bias_tolerance=1E-5
bias=ka.KeyProbDist(aliceKey)[1]
num_removals=0
start_time=time.time()

"""Remove bias until tolerance reached"""
while np.abs((bias-0.5))>bias_tolerance:
    bias=ka.KeyProbDist(newA)[1]
    newA,newB=kc.InduceBias(newA,newB,bias)
    num_removals+=1
end_time=time.time()
timespan=end_time-start_time

opt_bias=bias
opt_entropy=ka.SEntropy(ka.KeyProbDist(newA))


print("Remaining bias: {}".format(opt_bias))
print("Shannon entropy: {}".format(opt_entropy))
print("Bias removal time: {}".format(timespan))
print("Bias removal rate: {:.2f}ms/1000keys".format(1E6*timespan/aliceKey.size))
print("Number of bias removal cycles: {}".format(num_removals))
print("Efficiency: {}".format(len(newA)/len(aliceKey)))

aliceKey,bobKey = newA,newB



np.savetxt("keys\\tmp_aliceKey.txt",aliceKey, fmt='%d')
np.savetxt("keys\\tmp_bobKey.txt",bobKey, fmt='%d')
bmp.Encrypt("pics\\JKU.bmp","pics\\encrypted.bmp","keys\\tmp_AliceKey.txt")
bmp.Encrypt("pics\\encrypted.bmp","pics\\decrypted.bmp","keys\\tmp_bobKey.txt")