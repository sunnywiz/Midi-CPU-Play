using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using NAudio.Midi;

namespace MidiPlay
{
    class Program
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
                if (PlayingNote != null)
                {
                    _midiOut.Send(PlayingNote.OffEvent.GetAsShortMessage());
                }
                _midiOut.Send(nextNote.GetAsShortMessage());
                PlayingNote = nextNote;
            }
        }

        static void Main(string[] args)
        {
            int[] minorscale = new int[] { 0,2,3,5,7,8,10 };
            List<int> minorNotes = new List<int>(); 
            minorNotes.AddRange(minorscale.Select(x=>45-12+x));
            minorNotes.AddRange(minorscale.Select(x => 45 + x));
            minorNotes.AddRange(minorscale.Select(x => 45 + 12+ x));
            minorNotes.AddRange(minorscale.Select(x => 45 + 24 + x));
            try
            {
                int midiOutDevice = 0; // GS Wavetable Synth
                PerformanceCounter cpuCounter = new PerformanceCounter("Processor","% Processor Time","_Total");
                PerformanceCounter visualStudio = new PerformanceCounter("Process","% User Time","devenv");
                PerformanceCounter diskPercentCounter = new PerformanceCounter("LogicalDisk","% Disk Time","_Total");

                using (var midiOut = new MidiOut(midiOutDevice))
                {
                    NoteOnEvent lastNote = null;
                    PerfCounterScaling pcf1 = new PerfCounterScaling() {MaxValue = 100.0f};
                    PerfCounterScaling pcf2 = new PerfCounterScaling() {MaxValue = 100.0f};

                    var mtPiano = new MidiSingleNoteTracker(midiOut, 1, 0);
                    var mtStrings1 = new MidiSingleNoteTracker(midiOut, 2, 49);
                    
                    while (true)
                    {
                        var rawValue = cpuCounter.NextValue();
                        var cooked = pcf1.Scale(rawValue);
                        MapToNote(cooked, cooked, minorNotes, mtPiano, true);
                        System.Threading.Thread.Sleep(500);

                        try
                        {
                            var x = visualStudio.NextValue();
                            cooked = pcf1.Scale(x);
                            MapToNote(cooked, cooked, minorNotes, mtPiano, true); 
                        }
                        catch (Exception ex)
                        {
                            // ignore
                        }
                        System.Threading.Thread.Sleep(500);

                        rawValue = diskPercentCounter.NextValue();
                        cooked = pcf2.Scale(rawValue);
                        MapToNote(cooked, (cooked * 0.5f)+0.1f, minorNotes, mtStrings1, false);
                        System.Threading.Thread.Sleep(1000);

                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            finally
            {
                Console.WriteLine("Done");
            }
        }

        private static void MapToNote(float cookedNote, float cookedVelocity, List<int> minorNotes, MidiSingleNoteTracker mt, bool shouldAlwaysPlayNewNote = true)
        {
            // put range limiting in here
            var index = (int)(cookedNote * (minorNotes.Count - 1));
            var velocity = (int) (cookedVelocity * 90.0) + 30;
            var note = minorNotes[index];
            if (shouldAlwaysPlayNewNote)
            {
                mt.PlayNewNote(note, velocity);
            }
            else
            {
                if (mt.PlayingNote == null || mt.PlayingNote.NoteNumber != note)
                {
                    mt.PlayNewNote(note, velocity);
                }
            }
        }

        private static void MonitorPerProcessCPUUsage()
        {
            HashSet<string> wasDenied = new HashSet<string>();
            while (true)
            {
                var processes = System.Diagnostics.Process.GetProcesses();
                Console.WriteLine(processes.Length);
                for (int i = 0; i < processes.Length; i++)
                {
                    var process = processes[i];
                    if (wasDenied.Contains(process.ProcessName)) continue;
                    Console.Write(process.ProcessName);
                    Console.Write(" ");
                    try
                    {
                        Console.WriteLine(process.TotalProcessorTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        wasDenied.Add(process.ProcessName);
                    }
                }
                System.Threading.Thread.Sleep(2000);
                Console.WriteLine();
            }
        }

        private static void RandomlyPlaystuff()
        {
            int midiOutDevice = 0; // GS Wavetable Synth
            Random r = new Random();

            Dictionary<string, NoteOnEvent> currentlyPlaying = new Dictionary<string, NoteOnEvent>();


            using (var midiOut = new MidiOut(midiOutDevice))
            {
                // first lets set the patches for channels 1-9
                for (int i = 1; i <= 9; i++)
                {
                    var patchChange = new PatchChangeEvent(0, i, (i - 1) * 8);
                    midiOut.Send(patchChange.GetAsShortMessage());
                }
                // Okay, now lets figure out where our things are at

                for (int i = 0; i < 100; i++)
                {
                    int noteToPlay = r.Next(20, 100);
                    int velocityToPlay = r.Next(50, 100);
                    int channel = r.Next(1, 2); // channel indicates patch

                    string hash = $"{channel}";

                    NoteOnEvent existingNoteOn;
                    if (currentlyPlaying.TryGetValue(hash, out existingNoteOn))
                    {
                        midiOut.Send(existingNoteOn.OffEvent.GetAsShortMessage());
                        currentlyPlaying.Remove(hash);
                    }

                    var noteOn = new NoteOnEvent(0, channel, noteToPlay, velocityToPlay, 0);
                    midiOut.Send(noteOn.GetAsShortMessage());
                    Console.ReadLine();
                }
                foreach (var existingNote in currentlyPlaying.Values)
                {
                    midiOut.Send(existingNote.OffEvent.GetAsShortMessage());
                }
            }
        }

        private static void IterateMidiOutDevices()
        {
            for (int device = 0; device < MidiOut.NumberOfDevices; device++)
            {
                Console.WriteLine(MidiOut.DeviceInfo(device).ProductName);
            }
        }
    }
}
