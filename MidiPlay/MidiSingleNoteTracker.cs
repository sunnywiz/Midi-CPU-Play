using NAudio.Midi;

namespace MidiPlay
{
    partial class Program
    {
        public class MidiSingleNoteTracker
        {
            private readonly MidiOut _midiOut;
            private readonly int _channel;
            private readonly int _patch;

            public MidiSingleNoteTracker(MidiOut midiOut, int channel, int patch)
            {
                _midiOut = midiOut;
                _channel = channel;
                _patch = patch;

                midiOut.Send(new PatchChangeEvent(0, channel, patch).GetAsShortMessage());  // Flute
            }
            public NoteOnEvent PlayingNote { get; set; }

            public void PlayNewNote(int note, int velocity)
            {
                var nextNote = new NoteOnEvent(0, _channel, note, velocity, 0);
                StopPlaying();
                _midiOut.Send(nextNote.GetAsShortMessage());
                PlayingNote = nextNote;
            }

            public void StopPlaying()
            {
                if (PlayingNote != null)
                {
                    _midiOut.Send(PlayingNote.OffEvent.GetAsShortMessage());
                    PlayingNote = null; 
                }
            }
        }
    }
}
