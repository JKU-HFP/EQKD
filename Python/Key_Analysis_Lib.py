# -*- coding: utf-8 -*-
"""
Created on Tue Nov 12 16:19:04 2019

Methods for analizing keys from QKD

@author: Schimpf
"""

import numpy as np
from scipy import stats as st

def Qber(a,b):
    """
    Qubit error rate between two key sequences
    """
    tuples = list(zip(a,b))
    errs=list(filter(lambda x: int(x[0])^int(x[1]),tuples))
    return len(errs)/len(a)

def ProbDist(k,n,q):
    """ probability of k errors in a block with n size"""
    return st.binom.pmf(k,n,q)

def eta(n,q):
    """efficiency with blocksize of n"""
    return (1-ProbDist(1,n,q))*(n-1)/n

def QCorr(n,q):
    """Corrected QBER after parity correction"""
    return (1-ProbDist(0,n,q)-ProbDist(1,n,q))*2/n

def KeyQuality(aliceKey,aliceKeyNew,bobKeyNew):
    ratio = len(aliceKeyNew)/len(aliceKey)
    qber = Qber(aliceKeyNew,bobKeyNew)
    return ratio, qber

def KeyProbDist(key):
    hist = np.histogram(key,[0,1,2])[0]
    return hist/hist.sum()

def GetBias(key):
    return KeyProbDist(key)[-1]

def SEntropy(probDist):
    """Calculates the shannon entropy of a given Key probability distribution"""
    probDist=np.array(probDist)
    probDist=probDist[probDist!=0] #Get rid of zeros
    probDist=probDist/probDist.sum() #Normalize probability distribution    
    ent = (-probDist*np.log2(probDist)).sum()
    return ent

def ErrorCorrelation(aliceKey, bobKey, N=20):
    """Calculates error distance distribution"""
    
    errors = np.full_like(aliceKey,0)   
    
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