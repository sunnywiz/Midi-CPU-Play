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
        static void Main(string[] args)
        {
            try
            {
                int midiOutDevice = 0; // GS Wavetable Synth
                PerformanceCounter counter = new PerformanceCounter("Processor","% Processor Time","_Total");
                using (var midiOut = new MidiOut(midiOutDevice))
                {
                    NoteOnEvent lastNote = null; 
                    midiOut.Send(new PatchChangeEvent(0,1,74).GetAsShortMessage());  // Flute
                    float minValue = 0.0f;
                    float maxValue = 100.0f; 
                    while (true)
                    {
                        var rawValue = counter.NextValue();
                        if (rawValue < minValue) rawValue = minValue;
                        if (rawValue > maxValue) rawValue = maxValue;
                        var cooked = (rawValue - minValue) / (maxValue - minValue);   // 0..1

                        Console.WriteLine($"{rawValue:F2} {cooked:P}");

                        var velocity = (int) (cooked * 127.0);
                        var note = (int) (cooked * 24) + 40;
                        var nextNote = new NoteOnEvent(0, 1, note, velocity, 0); 
                        if (lastNote != null)
                        {
                            midiOut.Send(lastNote.OffEvent.GetAsShortMessage());
                        }
                        midiOut.Send(nextNote.GetAsShortMessage());
                        lastNote = nextNote; 
                        System.Threading.Thread.Sleep(2000);
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
