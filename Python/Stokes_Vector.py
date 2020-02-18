# -*- coding: utf-8 -*-
"""
Created on Tue Aug 20 12:57:05 2019

@author: Schimpf
"""

import numpy as np
import qutip as qt
from scipy.optimize import curve_fit
from matplotlib import pyplot as plt

def FromXRSP(filename):
    return np.loadtxt(filename, dtype=np.float64, delimiter='   ',skiprows=1,usecols=(1,2,3))

def I(b, I, M, C, S):
    
    delta = np.pi/2.0
    a = np.pi/2
    
    It = 1/2 * (I + (M*np.cos(2*b)+C*np.sin(2*b))*np.cos(2*(a-b)) +
                    ((C*np.cos(2*b)-M*np.sin(2*b))*np.cos(delta)+S*np.sin(delta))*np.sin(2*(a-b)) )
    
    return It


#measdat = np.loadtxt(r'C:\Users\BigLabPC\source\repos\JKU-HFP\EQKD\EQKDServer\bin\x64\Debug\Stokes.txt', dtype=np.float64, delimiter='\t', skiprows=1)

measdat = FromXRSP(r'I:\public\NANOSCALE SEMICONDUCTOR GROUP\1. DATA\BIG-LAB\2020\01\03\Stokes_V_shortFiber_H#Rot(deg)_0_358.spe0_Peak 1.TXT')
angles = measdat[:,0]*(np.pi/180)
powers = measdat[:,1]

popt, pcov = curve_fit(I, angles,powers, p0=[1,0.2,0.2,0.2])

fitdat=I(angles,popt[0],popt[1],popt[2],popt[3])

f=plt.figure(figsize=(7,3.55))
ax=f.add_subplot(111)

ax.plot(angles, powers, linewidth=0.5, marker='o', markersize=0.2, label="Measured data")
ax.plot(angles, fitdat, label="Fit")

ax.set_xlabel(r'Rotation angle ($\pi$)')
ax.legend()

f.show()

stokes, err = popt/popt[0], np.sqrt(np.diagonal(pcov))/popt[0]
dop = np.sqrt(stokes[1]**2+stokes[2]**2+stokes[3]**2)
doperr = 1/dop * np.sqrt(err[1]**2+err[2]**2+err[3]**2)

for i in [0,1,2,3]:
    print(r'S{0}: {1:0.3f} ({2:0.4f},{3:0.1f}%)'.format(i,stokes[i],err[i],100*err[i]/stokes[i]))
    
print(r'DoP: {0:0.3f} ({1:0.4f},{2:0.1f}%)'.format(dop,doperr,100*doperr/dop))

b=qt.Bloch()
b.xlabel= ['H','V']
b.ylabel= ['D','A']
b.zlabel= ['R','L'] 

pnt=[stokes[1],stokes[2],stokes[3]]
b.add_vectors(pnt)
b.show()
