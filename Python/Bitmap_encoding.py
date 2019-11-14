# -*- coding: utf-8 -*-
"""
Created on Tue Nov 12 14:56:17 2019

@author: Schimpf
"""

import sys

sys.path.append("E:\\Dropbox\\Dropbox\\Coding\\Resources\\pythonnet-2.4.0-rc2\\")
sys.path.append("DLLs\\")

import clr

clr.AddReference('QKD_Library')

import QKD_Encryption as qlib


def Encrypt(infile,outfile,keyfile):
    qlib.Encryption.EncryptAndSaveBMP(infile,outfile,keyfile)