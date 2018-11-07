namespace MidiPlay
{
    partial class Program
    {
        public class PerfCounterScaling
        {
            public float MinValue { get; set; } = 0.0f;
            public float MaxValue { get; set; } = 1.0f;

            public float Scale(float rawValue)
            {
                if (rawValue < MinValue) MinValue = rawValue;
                if (rawValue > MaxValue) MaxValue = rawValue;
                var cooked = (rawValue - MinValue) / (MaxValue - MinValue);   // 0..1
                return cooked; 
            }
        }
    }
}
