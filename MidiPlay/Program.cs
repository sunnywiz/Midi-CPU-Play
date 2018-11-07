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
    partial class Program
    {

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

                    PerfCounterScaling pcfCpu = new PerfCounterScaling() {MaxValue = 100.0f};
                    Averager cpuAvg = new Averager(10, 0.1f);
                    PerfCounterScaling pcuDisk = new PerfCounterScaling() {MaxValue = 100.0f};

                    var mtPiano = new MidiSingleNoteTracker(midiOut, 1, 0);
                    var mtStrings1 = new MidiSingleNoteTracker(midiOut, 2, 49);

                    while (true)
                    {
                        var cpu = GetNextScaledValueOrNull(cpuCounter, pcfCpu, cpuAvg);
                        var visualStudioCpu = GetNextScaledValueOrNull(visualStudio, pcfCpu);

                        if ((cpu.HasValue && (cpu > 0.5 || cpuAvg.AverageValue > 0.5)) ||
                            (visualStudioCpu.HasValue && visualStudioCpu > 0.5))
                        {
                            MapToNote(cpu.Value, cpuAvg.AverageValue, minorNotes, mtPiano, true);
                            System.Threading.Thread.Sleep(500);
                            if (visualStudioCpu.HasValue)
                            {
                                MapToNote(visualStudioCpu.Value, visualStudioCpu.Value, minorNotes, mtPiano, true);
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                        else
                        {
                            mtPiano.StopPlaying();
                            System.Threading.Thread.Sleep(1000);
                        }


                        var disk = GetNextScaledValueOrNull(diskPercentCounter, pcuDisk, null);
                        if (disk.HasValue)
                        {
                            MapToNote(disk.Value, 0.25f, minorNotes, mtStrings1, false);
                        }

                        Console.WriteLine($"cpu:{cpu:P}/{cpuAvg.AverageValue:P} vs:{visualStudioCpu:P} disk:{disk:P}");

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

        private static float? GetNextScaledValueOrNull(PerformanceCounter perfCounter, PerfCounterScaling scaler, Averager averager = null)
        {
            try
            {
                var rangedValue = perfCounter.NextValue();
                var scaledValue = scaler.Scale(rangedValue);
                if (averager != null)
                {
                    averager.Ingest(scaledValue); 
                }
                return scaledValue; 

            }
            catch (Exception ex)
            {
                return null;
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
