# -*- coding: utf-8 -*-
"""
Created on Tue Nov 19 15:30:46 2019

@author: Christian
"""
from matplotlib import pyplot as plt
import numpy as np
import scipy as sc
import time

import Key_Analysis_Lib as ka
import Key_Correction_Lib as kc
import Bitmap_encoding as bmp

from bitstring import BitArray

import sympy

alicefile="keys/SecureKey_Alice_2Tagger_long.txt"
bobfile="keys/SecureKey_Bob_2Tagger_long.txt"

aliceKey = np.loadtxt(alicefile)
bobKey = np.loadtxt(bobfile)

subKey=aliceKey[:1000]

b=BitArray(subKey)
buint=b.string


p=sympy.randprime(2**len(subKey),4*2**len(subKey))


hashInt=buint%p
hashBitArray=BitArray(uint=hashInt,length=np.log2(np.uint32(hashInt)))
