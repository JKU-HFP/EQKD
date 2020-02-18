# -*- coding: utf-8 -*-
"""
Created on Tue Oct 22 12:00:09 2019

@author: Schimpf
"""

from matplotlib import pyplot as plt
import numpy as np
from scipy import signal


path = r'I:\public\NANOSCALE SEMICONDUCTOR GROUP\1. DATA\BIG-LAB\2020\02\16\SA323P2_QD4\G2_X_PSSlit200_XX_renotched.txt'
data = np.loadtxt(path,delimiter=',')

vals=data[:,1]
peaks = signal.find_peaks(vals)

f = plt.figure(figsize=(6,4))
ax = f.add_subplot(111)

ax.plot(data[:,0]/1E3,data[:,1],color='blue')
ax.set_xlabel('Time delay (ns)')
ax.set_ylabel('Coincidences')



xvals=np.array(data[:,0])
yvals=np.array(data[:,1])

#center = 0
period = 12500
resolution = 128
peak_bin = 2000
sidepeak_range= [-4,-3,-2,-1,1,2,3,4]

indices_bin = peak_bin//(2*resolution)+1
middlePeakIndex = np.argmin(np.array(xvals)**2)
middlePeak_Area = yvals[middlePeakIndex-indices_bin:middlePeakIndex+indices_bin].sum()

peak_distance_indices = period//resolution

sidepeak_indices = [middlePeakIndex + i*peak_distance_indices for i in sidepeak_range]
sidepeak_areas = np.array(list(map(lambda x: yvals[x-indices_bin:x+indices_bin].sum(),sidepeak_indices)))

print("Middle peak area ratio: " + str(middlePeak_Area/np.average(sidepeak_areas)))

