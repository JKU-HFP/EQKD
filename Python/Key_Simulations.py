# -*- coding: utf-8 -*-
"""
Created on Tue Nov 12 16:21:42 2019

@author: Schimpf
"""

import os
from matplotlib import pyplot as plt
import numpy as np
import time

import Key_Analysis_Lib as ka
import Key_Correction_Lib as kc
import Bitmap_encoding as bmp
from matplotlib import image as im
import imageio

alicefile="keys\SecureKey_Alice_2Tagger_long.txt"
bobfile="keys\SecureKey_Bob_2Tagger_long.txt"

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

opt_bias=bias
opt_entropy=ka.SEntropy(ka.KeyProbDist(newA))

print("Remaining bias: {}".format(opt_bias))
print("Shannon entropy: {}".format(opt_entropy))
print("Bias removal time: {}".format(end_time-start_time))
print("Number of bias removal cycles: {}".format(num_removals))
print("Efficiency: {}".format(len(newA)/len(aliceKey)))

aliceKey,bobKey = newA,newB



print("--------- Induce different biases -------------")

sim_biases = [0.01*b for b in range(101)]
sim_entropies = list(map(lambda p: ka.SEntropy([1-p,p]),sim_biases)) 

gifdir="tmp/"
if not os.path.isdir(gifdir): os.mkdir(gifdir)

picfiles=[]
for i,bias in enumerate(sim_biases[::2]):
    newA,newB=kc.RemoveBias(aliceKey,bobKey,bias)
    np.savetxt("keys\\tmp_aliceKey.txt",newA, fmt='%d')
    np.savetxt("keys\\tmp_bobKey.txt",newB, fmt='%d')
    bmp.Encrypt("pics\\JKU.bmp","pics\\encrypted.bmp","keys\\tmp_AliceKey.txt")
    bmp.Encrypt("pics\\encrypted.bmp","pics\\decrypted.bmp","keys\\tmp_bobKey.txt")
    
    entr_fig = plt.figure(figsize=(10,5))

    entr_ax1 = entr_fig.add_subplot(221)
    entr_ax1.plot(sim_biases,sim_entropies)
    entr_ax1.plot([ka.GetBias(newA),],[ka.SEntropy(ka.KeyProbDist(newA)),],ls='none',marker='s',markersize=5)
    entr_ax1.set_xlabel('Probability of Key-Bit=1')
    entr_ax1.set_ylabel('Shannon entropy')
    
    bmp_ax1 = entr_fig.add_subplot(223)
    bmp_ax1.imshow(im.imread("pics\\encrypted.bmp"))
    
    bmp_ax2 = entr_fig.add_subplot(224)
    bmp_ax2.imshow(im.imread("pics\\decrypted.bmp"))
    
    entr_fig.tight_layout()
    picfile=gifdir+str(i)+".png"
    picfiles.append(picfile)
    entr_fig.savefig(picfile)
    entr_fig.show()

"""Make gif"""
images=[]
for filename in picfiles:
    images.append(imageio.imread(filename))
imageio.mimsave(gifdir+"entropy.gif", images, fps=3)

#
#
#"""
#Simulation of error probability distribution over Blocksize n
#for fixed QBER
#"""
#n=16
#qb=0.025
#
#ran=[k for k in range(0,n+1)]
#
#qbfig = plt.figure(figsize=(10,5))
#
#ax=qbfig.add_subplot(111)
#
#ax.plot(ka.ProbDist(ran,n,qb),marker='o')
#ax.set_title('n = {}'.format(n))
#ax.set_xticks(ran)
#ax.set_xlabel('number of errors')
#ax.set_ylabel('Prob. density')
#  
#qbfig.tight_layout()
#qbfig.show()
#
#
#"""
#Simulation of error probability distribution over QBER
#and blocke size
#"""
#n=8
#
#ran=[k for k in range(0,20)]
#qbran=[q*0.02 for q in range(0,15)]
#
#qbfig = plt.figure(figsize=(10,5))
#qbfig.suptitle("Blocksize: {}".format(n))
#
#for i, qb in enumerate(qbran):
#    ax=qbfig.add_subplot(len(qbran)//4 +1, 4,i+1)
#    
#    ax.plot(ka.ProbDist(ran,n,qb),marker='o')
#    ax.set_title('Qber: {:.2f}%'.format(qb*100.0))
#    ax.set_xticks(ran)
#  
#qbfig.tight_layout()
#qbfig.show()
#
#"""
#Probability that error occures AND is detected
#"""
#errran=[q*0.001 for q in range(1,500)]
#
#PerrDet = lambda n,q: sum(ka.ProbDist(ran[1::2],n,q))/n
#
#errfig = plt.figure(figsize=(10,5))
#axerr = errfig.add_subplot(121)
#axerr.set_xlabel("QBER (%)")
#axerr.set_ylabel("Error detection probability (%)")
#axerr.set_xlim((0,30))
#axerr.vlines(2.5,0,12,linestyles='dashed')
#
#for n in [4,8,12,16]:
#    PerrDetSim = list(map(lambda q: PerrDet(n,q)*100,errran))
#    axerr.plot(list(map(lambda x: x*100.0,errran)),PerrDetSim,label='n={}'.format(n))
#
#axerr.legend()  
#
#axgrad = errfig.add_subplot(122)
#axgrad.set_xlabel("QBER (%)")
#axgrad.set_ylabel("Sensitivity (% per % QBER)")
#axgrad.set_xlim((0,30))
#axgrad.vlines(2.5,0,0.1,linestyles='dashed')
#
#for n in [4,8,12,16]:
#    GradSim = np.gradient(list(map(lambda q: PerrDet(n,q)*100,errran)))
#    axgrad.plot(list(map(lambda x: x*100.0,errran)),GradSim,label='n={}'.format(n))
#
#axgrad.legend()  
#
#errfig.tight_layout()
#errfig.show()
#
#"""
#Simulation of efficiency and resulting QBER + Comparison with measured data
#"""
#
#alicefile="..\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long_unbiased.txt"
#bobfile="..\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long_unbiased.txt"
#
#aliceKey = np.loadtxt(alicefile)
#bobKey = np.loadtxt(bobfile)
#
#QBER=ka.Qber(aliceKey,bobKey)
#
#nran=[n for n in range(2,50)]
#
#etaSim=list(map(lambda n: ka.eta(n,QBER),nran))
#qberSim=ka.QCorr(nran,QBER)
#
#etaReal = []
#qberReal = []
#
#aShuff,bShuff = kc.ShuffleKeys(aliceKey,bobKey) #Shuffle keys to eliminate correlations
#
#
#for n in nran: 
#    aliceKeyNew, bobKeyNew = kc.ParityCorrect(aShuff,bShuff,n)
#    e, qber = ka.KeyQuality(aliceKey,aliceKeyNew,bobKeyNew)
#    etaReal.append(e)
#    qberReal.append(qber)
#
#f = plt.figure(figsize=(10,5))
#f.suptitle("Original QBER: {:.2f}".format(QBER*100))
#
#ax1=f.add_subplot(121)
#
#ax1.plot(nran,etaSim,label='Model')
#ax1.plot(nran,etaReal,marker="o",label='Measured key')
#ax1.set_xlabel('n')
#ax1.set_ylabel('Key efficiency')
#ax1.set_xticks(nran)
#ax1.legend()
#ax1.grid(True)
#
#ax2=f.add_subplot(122)
#ax2.plot(nran,qberSim*100,label='Model')
#ax2.plot(nran,list(map(lambda x: x*100.0,qberReal)),marker="o",label='Measured key')
#ax2.set_xlabel('n')
#ax2.set_ylabel('resulting QBER (%)')
#ax2.set_xticks(nran)
#ax2.legend()
#ax2.grid(True)
#
#f.tight_layout()
#f.show()
#
#
#
#"""
#Generate new key by parity correction
#"""
#
#aliceKey,bobKey = kc.ShuffleKeys(aliceKey,bobKey) #Shuffle keys to eliminate correlations
#aliceKeyNew, bobKeyNew = kc.ParityCorrect(aliceKey,bobKey,10)
#print("Key ratio: {}".format(len(aliceKeyNew)/len(aliceKey)))
#print("Original QBER: {:.3f}%".format(100*ka.Qber(aliceKey,bobKey)))
#print("New QBER: {:.3f}%".format(100*ka.Qber(aliceKeyNew,bobKeyNew)))
#
##np.savetxt("E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Alice_2Tagger_long_unbiased_parityCorr_2.txt",aliceKeyNew, fmt='%d') 
##np.savetxt("E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\SecureKey_Bob_2Tagger_long_unbiased_parityCorr_2.txt",bobKeyNew, fmt='%d') 
#
#
#"""
#Simulation of QBER when eavesdropped
#"""

