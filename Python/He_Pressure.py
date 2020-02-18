# -*- coding: utf-8 -*-
"""
Created on Mon Jan 13 08:50:35 2020

@author: Schimpf
"""

import numpy as np

def V(A,B,p,d,gamma):
    return B*p*d/(np.log(A*p*d)-np.log(np.log(1+1/gamma)))
    
A=2.25
B=25.5
d=0.0003
gamma=4.6E-2

p=0.005E-5 #Pascal

print(V(A,B,p,d,gamma))