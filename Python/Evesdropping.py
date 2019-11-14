# -*- coding: utf-8 -*-
"""
Created on Tue Nov 12 16:17:05 2019

@author: Schimpf
"""

import numpy as np
from matplotlib import pyplot as plt

def R(theta):
    c, s = np.cos(theta), np.sin(theta)
    return np.array(((c,-s), (s, c)))

def qber_eve(partial, theta):
    """
    Gives theoretical QBER when perfect phi+ is partially projected by polarizer
    """
    #Bases
    h=np.matrix([[1],[0]])
    v=np.matrix([[0],[1]])
    d=1/np.sqrt(2)*(h+v)
    a=1/np.sqrt(2)*(h-v)
    
    hh=np.kron(h,h)
    vv=np.kron(v,v)
    hv=np.kron(h,v)
    vh=np.kron(v,h)
    da=np.kron(d,a)
    ad=np.kron(a,d)
    
    psi = 1/np.sqrt(2) * (hh + vv)
    
    polarizer=R(theta) @ [[1,0],[0,partial]] @ R(-theta)
    #proj =np.kron(np.eye(2),polarizer).astype(float)
    proj =np.kron([[1,0],[0,partial]],polarizer)
    
    projPsi=proj @ psi
    projPsiNorm = projPsi/(np.sqrt(projPsi.H @ projPsi))
    #print(projPsiNorm)
    
    phv=np.abs(hv.H @ projPsiNorm)**2
    pvh=np.abs(vh.H @ projPsiNorm)**2
    pda=np.abs(da.H @ projPsiNorm)**2
    pad=np.abs(ad.H @ projPsiNorm)**2
    simQBER =1/2*(phv+pvh+pda+pad)[0,0]
    #print(simQBER)
    return simQBER

SimQbersEve = np.array([[p/100,qber_eve(p/100,0)] for p in range(0,105,5)])

f = plt.figure(figsize=(7,4))
ax = f.add_subplot(111)

ax.plot(SimQbersEve[:,0],SimQbersEve[:,1]*100)
ax.set_xlabel('T')
ax.set_ylabel('Theoretical QBER (%)')

f.show()