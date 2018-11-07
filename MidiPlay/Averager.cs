using System;

namespace MidiPlay
{
    internal class Averager 
    {
        private readonly int _numSamplesToAverageOver;

        public float CurrentValue { get; private set; }
        private float _totalAverageValue; 

        public Averager(int numSamplesToAverageOver, float initialValue)
        {
            if (numSamplesToAverageOver<1) throw new NotSupportedException("must be at least 1 sample");
            _numSamplesToAverageOver = numSamplesToAverageOver;
            CurrentValue = initialValue;
            _totalAverageValue = initialValue * numSamplesToAverageOver; 
        }


        public void Ingest(float scaledValue)
        {
            CurrentValue = scaledValue;
            _totalAverageValue = ((_totalAverageValue * (_numSamplesToAverageOver - 1)) /
                                  _numSamplesToAverageOver) + scaledValue; 
        }

        public float AverageValue => _totalAverageValue / _numSamplesToAverageOver; 
    }
}