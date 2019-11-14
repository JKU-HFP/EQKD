# -*- coding: utf-8 -*-
"""
Created on Mon Oct 14 09:59:48 2019

@author: Schimpf
"""

import numpy as np
from scipy import stats
from mayavi import mlab
from matplotlib import pyplot as plt

#Function
def R(theta):
    c, s = np.cos(theta), np.sin(theta)
    return np.array(((c,-s), (s, c)))

def QWP(theta):
    return np.matmul(np.matmul(R(theta),1/np.sqrt(2)*np.complex64([[1-1j,0],[0,1+1j]])),R(-theta))

def HWP(theta):
    return np.matmul(np.matmul(R(theta),[[1,0],[0,-1]]),R(-theta)) 

#Bases
h=[1,0]
v=[0,1]
d=1/np.sqrt(2)*np.float64([1,1])
a=1/np.sqrt(2)*np.float64([1,-1])

hh=np.kron(h,h)
vv=np.kron(v,v)
hv=np.kron(h,v)
vh=np.kron(v,h)
da=np.kron(d,a)
ad=np.kron(a,d)


def p(theta1,theta2,theta3):
    #Pol correction transformation
    psi = 1/np.sqrt(2) * (hh + vv)
    
    psi_Corr = np.matmul( np.kron(np.matmul( np.matmul(QWP(theta3),HWP(theta2)), QWP(theta1)), np.eye(2)), psi)
    
    return 1/2*(np.abs(np.dot(hv,psi_Corr))**2 + np.abs(np.dot(vh,psi_Corr))**2 + np.abs(np.dot(da,psi_Corr))**2 + np.abs(np.dot(ad,psi_Corr))**2)
 
numpoints=20
x=y=z=np.linspace(0,np.pi,num=numpoints)
dens = np.zeros((len(x),len(y),len(z)))


for xi, xv in enumerate(x):
    for yi, yv in enumerate(y):
        for zi, zv in enumerate(z):
            dens[xi,yi,zi]=p(xv,yv,zv)
        
# Plot scatter with mayavi
figure = mlab.figure('DensityPlot')

xmin, ymin, zmin = x.min(), y.min(), z.min()
xmax, ymax, zmax = x.max(), y.max(), z.max()
xi, yi, zi = np.mgrid[xmin:xmax:numpoints*1j, ymin:ymax:numpoints*1j, zmin:zmax:numpoints*1j]

grid = mlab.pipeline.scalar_field(xi,yi,zi, dens)
min = dens.min()
max= dens.max()
mlab.pipeline.volume(grid, vmin=min, vmax=min + .5*(max-min))

mlab.axes(xlabel='QWP1',ylabel='HWP',zlabel='QWP2')
mlab.show()

#Show slice
fig = plt.figure(figsize=(3,2.5))

xslice,zslice = np.meshgrid(x,z)

for i in range(8):
    ax = fig.add_subplot(4,2,i+1)#, projection='3d')
    cont=ax.contourf(xslice,zslice,dens[:,numpoints//8 * i,:])
    plt.colorbar(cont, ax=ax, extend='both')
    
    ax.set_title('HWP={:.2f}'.format(y[numpoints//8 *i]),fontsize=7)
    ax.set_xlabel(r'QWP1',fontsize=7)
    ax.set_ylabel(r'QWP2',fontsize=7)
    #ax.set_ylim((1E-9,1E-3))
    #ax.set_yscale("log")
    ax.tick_params(axis='both',which="both",direction='in',labelsize=7)
 
#plt.subplots_adjust(top=0.94,bottom=0.15,left=0.117,right=0.946)
plt.tight_layout()
plt.show()

"""
mu, sigma = 0, 0.1 
x = 10*np.random.normal(mu, sigma, 5000)
y = 10*np.random.normal(mu, sigma, 5000)    
z = 10*np.random.normal(mu, sigma, 5000)

xyz = np.vstack([x,y,z])
kde = stats.gaussian_kde(xyz)

# Evaluate kde on a grid
xmin, ymin, zmin = x.min(), y.min(), z.min()
xmax, ymax, zmax = x.max(), y.max(), z.max()
xi, yi, zi = np.mgrid[xmin:xmax:30j, ymin:ymax:30j, zmin:zmax:30j]
coords = np.vstack([item.ravel() for item in [xi, yi, zi]]) 
density = kde(coords).reshape(xi.shape)

# Plot scatter with mayavi
figure = mlab.figure('DensityPlot')

grid = mlab.pipeline.scalar_field(xi, yi, zi, density)
min = density.min()
max=density.max()
mlab.pipeline.volume(grid, vmin=min, vmax=min + .5*(max-min))

mlab.axes()
mlab.show()
"""