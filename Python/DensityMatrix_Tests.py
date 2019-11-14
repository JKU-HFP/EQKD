# -*- coding: utf-8 -*-
"""
Created on Thu Oct 24 11:51:03 2019

@author: Schimpf
"""

import numpy as np

h=np.array([[1],[0]])
v=np.array([[0],[1]])
d=1/np.sqrt(2)*(h+v)
a=1/np.sqrt(2)*(h-v)
r=1/np.sqrt(2)*(h+1j*v)

deg=np.pi/180

def R(theta):
    c, s = np.cos(theta), np.sin(theta)
    return np.array(((c,-s), (s, c)))

def HWP(theta):
    return R(theta)@np.array([[1,0],[0,-1]])@R(-theta)

def QWP(theta):
    return R(theta)@np.array([[1,0],[0,1j]])@R(-theta)


dB=QWP(0*deg)@(HWP(22.5*deg)@v)
aB=QWP(0*deg)@(HWP(22.5*deg)@(HWP(0)@v))
print(np.abs((np.asmatrix(aB).H@dB)[0,0])**2)
