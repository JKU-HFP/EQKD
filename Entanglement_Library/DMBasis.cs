﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;

namespace Entanglement_Library
{
    internal class DMBasis
    {
        private Kurolator _correlator;
       
        public ulong TimeBin { get; set; } = 1000;

        public double[] BasisConfig { get; set; }
        public Histogram CrossCorrHistogram { get; private set; }
        public List<Peak> Peaks { get; private set; }

        public DMBasis(double[] basisconfig, uint chanA, uint chanB, ulong timewindow)
        {
            BasisConfig = basisconfig;

            CrossCorrHistogram = new Histogram(new List<(byte A, byte B)> { ((byte)chanA, (byte)chanB) }, timewindow);

            _correlator = new Kurolator(new List<CorrelationGroup> { CrossCorrHistogram }, timewindow);
        }

        public void CreateHistogram(List<TimeTags> tt, long offsetB)
        {
            tt.ForEach(tags => _correlator.AddCorrelations(tags, tags, offsetB));

            Peaks = CrossCorrHistogram.GetPeaks(6250, 0.1, true, TimeBin);
        }
    }
}