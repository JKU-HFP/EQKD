# -*- coding: utf-8 -*-
"""
Created on Tue Oct 22 12:00:09 2019

@author: Schimpf
"""

from matplotlib import pyplot as plt
import numpy as np
from scipy import signal

path = r'E:\Dropbox\Dropbox\PhD\QKD\QKD_2Taggers_10_10_2019\StateCorrection_2_BruteForce\HD.txt'
data = np.loadtxt(path,delimiter=',')

vals=data[:,1]
peaks = signal.find_peaks(vals)

f = plt.figure(figsize=(6,4))
ax = f.add_subplot(111)

ax.plot(data[:,0]/1E3,data[:,1],color='blue')
ax.set_xlabel('Time delay (ns)')
ax.set_ylabel('Coincidences')
