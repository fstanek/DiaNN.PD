namespace DiaNN.PD.Models
{
    public class Spectrum
    {
        public int SpectrumId { get; set; }
        public int FileId { get; set; }
        public double Intensity { get; set; }
        public double MassOverCharge { get; set; }
        public short Charge { get; set; }
    }
}