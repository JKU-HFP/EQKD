# -*- coding: utf-8 -*-
"""
Created on Tue Nov 12 14:56:17 2019

@author: Schimpf
"""
import clr
import sys

sys.path.append("DLLs\\")
clr.AddReference('QKD_Library')

import QKD_Encryption as qlib

def Encrypt(infile,outfile,keyfile):
    qlib.Encryption.EncryptAndSaveBMP(infile,outfile,keyfile)