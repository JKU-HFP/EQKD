# -*- coding: utf-8 -*-
"""
Created on Fri Nov 15 13:21:49 2019

@author: Christian
"""

import hashlib

hashob = hashlib.blake2b(digest_size=10)
hashob.update(b"teststring")
print(hashob.hexdigest())

