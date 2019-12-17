# -*- coding: utf-8 -*-
"""
Created on Sat Dec 14 11:38:51 2019

@author: Christian
"""

import scipy.stats as st
import numpy as np
from matplotlib import pyplot as plt

lambdas=[i*0.1 for i in range(31)]

effs=1-st.poisson.cdf(1,mu=lambdas)
fids=1/4+3/4*(1-effs)

f = plt.figure(figsize=(3,4))
ax=f.add_subplot(111)

ax.plot(effs,fids)
ax.set_xscale('log')
ax.set_xlim((1E-6,1))
ax.set_ylim((0.5,1))

f.show()