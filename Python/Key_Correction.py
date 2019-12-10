# -*- coding: utf-8 -*-
"""
Created on Wed Nov 13 13:50:12 2019

@author: Schimpf

Correction of existing key obtained by QKD
"""


from matplotlib import pyplot as plt
import numpy as np
import scipy as sc

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

f=plt.figure(figsize=(3,2))
ax=f.add_subplot(111)
klist=[0,1,2,3]
plist=ka.ProbDist(klist,1000,ka.Qber(aliceKey,bobKey))
ax.bar(klist,plist*100)
ax.set_xlabel('Number of errors')
ax.set_ylabel('Probability (%)')
ax.set_ylim((0,10))
f.tight_layout()
f.show()

# If we were to simply plot pts, we'd lose most of the interesting
# details due to the outliers. So let's 'break' or 'cut-out' the y-axis
# into two portions - use the top (ax) for the outliers, and the bottom
# (ax2) for the details of the majority of our data
f, (ax, ax2) = plt.subplots(2, 1, sharex=True, figsize=(3,2))

# plot the same data on both axes
bar1=ax.bar(klist,plist*100)
bar2=ax2.bar(klist,plist*100)

# zoom-in / limit the view to different portions of the data
ax.set_ylim(78, 100)  # outliers only
ax2.set_ylim(0, 22)  # most of the data

# hide the spines between ax and ax2
ax.spines['bottom'].set_visible(False)
ax2.spines['top'].set_visible(False)
ax.xaxis.tick_top()
ax.tick_params(labeltop=False)  # don't put tick labels at the top
ax2.xaxis.tick_bottom()
ax2.set_xlabel('Number of errors')
ax2.set_ylabel('Probability (%)')

d = .015  # how big to make the diagonal lines in axes coordinates
# arguments to pass to plot, just so we don't keep repeating them
kwargs = dict(transform=ax.transAxes, color='k', clip_on=False)
ax.plot((-d, +d), (-d, +d), **kwargs)        # top-left diagonal
ax.plot((1 - d, 1 + d), (-d, +d), **kwargs)  # top-right diagonal

kwargs.update(transform=ax2.transAxes)  # switch to the bottom axes
ax2.plot((-d, +d), (1 - d, 1 + d), **kwargs)  # bottom-left diagonal
ax2.plot((1 - d, 1 + d), (1 - d, 1 + d), **kwargs)  # bottom-right diagonal

f.show()


aliceKey,bobKey=twiggleResult["keyA"],twiggleResult["keyB"]



np.savetxt("keys\\tmp_aliceKey.txt",aliceKey, fmt='%d')
np.savetxt("keys\\tmp_bobKey.txt",bobKey, fmt='%d')
bmp.Encrypt("pics\\JKU.bmp","pics\\encrypted.bmp","keys\\tmp_AliceKey.txt")
bmp.Encrypt("pics\\encrypted.bmp","pics\\decrypted.bmp","keys\\tmp_bobKey.txt")