using DiaNN.PD.Models;
using System.Collections.Generic;
using Thermo.Magellan.BL.Data;
using Thermo.Magellan.MassSpec;

namespace DiaNN.PD.Services
{
    /// <summary>
    /// Allows mapping from raw scan numbers to PD-internal spectrum IDs
    /// </summary>
    public class SpectrumMapper
    {
        private readonly Dictionary<string, int> fileMap;
        private readonly Dictionary<(int fileId, int scanNumber), Spectrum> spectrumMap;    // TODO store MassSpectrum as value instead?

        public SpectrumMapper()
        {
            fileMap = new Dictionary<string, int>();
            spectrumMap = new Dictionary<(int fileId, int scanNumber), Spectrum>();
        }

        public void Add(IEnumerable<SpectrumFile> spectrumFiles)
        {
            foreach (var spectrumFile in spectrumFiles)
                fileMap.Add(spectrumFile.FullPhysicalFileName, spectrumFile.SpectrumFileID);
        }

        public void Add(IEnumerable<MassSpectrum> spectra)
        {
            foreach (var spectrum in spectra)
            {
                foreach (var scanNumber in spectrum.Header.ScanNumbers)
                {
                    var key = (spectrum.Header.FileID, scanNumber);
                    var value = GetSpectrum(spectrum);
                    spectrumMap.Add(key, value);
                }
            }
        }

        private Spectrum GetSpectrum(MassSpectrum massSpectrum)
        {
            return new Spectrum
            {
                SpectrumId = massSpectrum.Header.SpectrumID,
                FileId = massSpectrum.Header.FileID,
                Intensity = massSpectrum.Precursor.Intensity,
                MassOverCharge = massSpectrum.Precursor.InstrumentDeterminedMonoisotopicMass.Mass,  // TODO is the correct value?
                Charge = massSpectrum.Precursor.Charge,
            };
        }

        public bool TryGetSpectrumId(string fileName, int scanNumber, out Spectrum spectrum)
        {
            spectrum = GetSpectrumId(fileName, scanNumber);
            return spectrum != null;
        }

        public Spectrum GetSpectrumId(string fileName, int scanNumber)
        {
            if (fileMap.TryGetValue(fileName, out var fileId) && spectrumMap.TryGetValue((fileId, scanNumber), out var spectrum))
                return spectrum;
            else
                return default;
        }
    }
}