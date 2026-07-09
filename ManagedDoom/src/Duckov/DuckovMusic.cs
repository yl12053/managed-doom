using System;
using System.IO;
using System.Runtime.InteropServices;
using Duckov;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using ManagedDoom.Audio;
using MeltySynth;
using Debug = UnityEngine.Debug;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace ManagedDoom.Duckov
{
    public class DuckovMusic: IMusic, IDisposable
    {
        private Config config;
        private Wad wad;

        private MusStream stream;
        private Bgm current;

        public DuckovMusic(Config config, GameContent content, string sfPath)
        {
            try
            {
                Debug.Log("Initialize music: ");

                this.config = config;
                this.wad = content.Wad;

                stream = new MusStream(this, config, sfPath);
                current = Bgm.NONE;

                Debug.Log("OK");
            }
            catch (Exception e)
            {
                Debug.Log("Failed");
                Dispose();
                Debug.LogError(e);
            }
        }

        public void StartMusic(Bgm bgm, bool loop)
        {
            if (bgm == current)
            {
                return;
            }

            var lump = "D_" + DoomInfo.BgmNames[(int)bgm].ToString().ToUpper();
            var data = wad.ReadLump(lump);
            var decoder = ReadData(data, loop);
            stream.SetDecoder(decoder);

            current = bgm;
        }

        private IDecoder ReadData(byte[] data, bool loop)
        {
            var isMus = true;
            for (var i = 0; i < MusDecoder.MusHeader.Length; i++)
            {
                if (data[i] != MusDecoder.MusHeader[i])
                {
                    isMus = false;
                }
            }

            if (isMus)
            {
                return new MusDecoder(data, loop);
            }

            var isMidi = true;
            for (var i = 0; i < MidiDecoder.MidiHeader.Length; i++)
            {
                if (data[i] != MidiDecoder.MidiHeader[i])
                {
                    isMidi = false;
                }
            }

            if (isMidi)
            {
                return new MidiDecoder(data, loop);
            }

            throw new Exception("Unknown format!");
        }

        public void Dispose()
        {
            Debug.Log("Shutdown music.");

            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }

        public int MaxVolume
        {
            get
            {
                return 15;
            }
        }

        public int Volume
        {
            get
            {
                return config.audio_musicvolume;
            }

            set
            {
                config.audio_musicvolume = value;
            }
        }



        private class MusStream : IDisposable
        {
            private static readonly int latency = 200;
            private static readonly int blockLength = 2048;

            private DuckovMusic parent;
            private Config config;

            private Synthesizer synthesizer;

            private float[] left;
            private float[] right;

            private volatile IDecoder current;
            private volatile IDecoder reserved;

            private EventInstance? eventInstance = null;
            
            public MusStream(DuckovMusic parent, Config config, string sfPath)
            {
                this.parent = parent;
                this.config = config;

                config.audio_musicvolume = Math.Clamp(config.audio_musicvolume, 0, parent.MaxVolume);

                var settings = new SynthesizerSettings(MusDecoder.SampleRate);
                settings.BlockSize = MusDecoder.BlockLength;
                settings.EnableReverbAndChorus = config.audio_musiceffect;
                synthesizer = new Synthesizer(sfPath, settings);

                left = new float[blockLength];
                right = new float[blockLength];

                // audioStream = new AudioStream(device, MusDecoder.SampleRate, 2, true, latency, blockLength);
                // todo: stream here
            }

            public void CreateNewInstance()
            {
                if (!AudioManager.TryCreateEventInstance("Music/custom", out var eventInstance))
                {
                    Debug.LogError("Failed to create music event");
                    return;
                }

                eventInstance.setCallback(HandleEvent);
                eventInstance.start();
                this.eventInstance = eventInstance;
            }

            private float[] bufferleft = new float[blockLength * 2];
            private int bufferCopyFrom = -1;

            public static RESULT HandleCallback(IntPtr soundPtr, IntPtr data, uint datalen)
            {
                FMOD.Sound sound = new FMOD.Sound(soundPtr);
                
                sound.getUserData(out IntPtr userDataPtr);
                
                GCHandle handle = GCHandle.FromIntPtr(userDataPtr);
                
                var player = (MusStream) handle.Target;
                unsafe
                {
                    Debug.Log("Start write data");
                    Debug.Log("data ptr = " + data);
                    Debug.Log("datalen = " + datalen);
                    if (datalen == 0) return RESULT.OK;
                    float* buffer = (float*)data;
                    long ssize = datalen / 4;
                    long ptr = 0;
                    if (player.bufferCopyFrom != -1)
                    {
                        Debug.Log("Has hist data");
                        for (;
                             player.bufferCopyFrom < blockLength * 2 && ssize > 0;
                             player.bufferCopyFrom += 1, ssize -= 1, ptr += 1)
                        {
                            Debug.Log("Writing to ptr+" + ptr);
                            buffer[ptr] = player.bufferleft[player.bufferCopyFrom];
                        }

                        if (player.bufferCopyFrom >= blockLength * 2) player.bufferCopyFrom = -1;
                    }

                    while (ssize >= blockLength * 2)
                    {
                        Debug.Log("Writing to ptr[]+" + ptr);
                        player.OnGetData(new Span<float>(buffer + ptr, blockLength * 2));
                        ptr += blockLength * 2;
                        ssize -= blockLength * 2;
                    }

                    if (ssize >= 0)
                    {
                        Debug.Log("Writing to buffer");
                        player.OnGetData(player.bufferleft.AsSpan());
                        player.bufferCopyFrom = 0;
                        for (;
                             player.bufferCopyFrom < blockLength * 2 && ssize > 0;
                             player.bufferCopyFrom += 1, ssize -= 1, ptr += 1)
                        {
                            Debug.Log("Writing to ptr+" + ptr);
                            buffer[ptr] = player.bufferleft[player.bufferCopyFrom];
                        }
                    }
                }

                return RESULT.OK;
            }
            
            public RESULT HandleEvent(EVENT_CALLBACK_TYPE type, IntPtr _event, IntPtr parameters)
            {
                switch (type)
                {
                    case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                    {
                        MODE mode = MODE.LOOP_NORMAL | MODE.CREATECOMPRESSEDSAMPLE | MODE.NONBLOCKING;
                        PROGRAMMER_SOUND_PROPERTIES structure = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameters, typeof(PROGRAMMER_SOUND_PROPERTIES));

                        var _handle = GCHandle.Alloc(this, GCHandleType.Normal);
                        IntPtr contextPtr = GCHandle.ToIntPtr(_handle);
                        
                        CREATESOUNDEXINFO exinfo = new()
                        {
                            cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
                            format = SOUND_FORMAT.PCMFLOAT,
                            defaultfrequency = MusDecoder.SampleRate,
                            decodebuffersize = (uint) blockLength,
                            length = (uint) blockLength * 2 * sizeof(float),
                            numchannels = 2,
                            pcmreadcallback = HandleCallback,
                            pcmsetposcallback = (IntPtr sound, int subsound, uint position, TIMEUNIT postype) => RESULT.OK,
                            userdata = contextPtr
                        };
                        RuntimeManager.CoreSystem.createStream("Sound_DOOM_Music", MODE.OPENUSER | MODE.LOOP_NORMAL,
                            ref exinfo, out var sound);
                        
                        structure.sound = sound.handle;
                        structure.subsoundIndex = -1;
                        Marshal.StructureToPtr(structure, parameters, fDeleteOld: false);
                        break;
                    }
                    case EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                        new Sound(((PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameters, typeof(PROGRAMMER_SOUND_PROPERTIES))).sound).release();
                        break;
                    case EVENT_CALLBACK_TYPE.DESTROYED:
                        break;
                }
                return RESULT.OK;
            }

            public void SetDecoder(IDecoder decoder)
            {
                reserved = decoder;

                if (eventInstance != null)
                {
                    ((EventInstance)eventInstance).getPlaybackState(out var state);
                    if (state != PLAYBACK_STATE.STOPPED) return;
                }
                
                CreateNewInstance();

                /* if (audioStream.State == PlaybackState.Stopped)
                {
                    audioStream.Play(OnGetData);
                } */
            }

            private void OnGetData(Span<float> samples)
            {
                if (reserved != current)
                {
                    synthesizer.Reset();
                    current = reserved;
                }

                var a = 2.0F * config.audio_musicvolume / parent.MaxVolume;

                current.RenderWaveform(synthesizer, left, right);

                var pos = 0;

                for (var t = 0; t < blockLength; t++)
                {
                    var sampleLeft = Math.Clamp(a * left[t], -1f, 1f);
                    var sampleRight = Math.Clamp(a * right[t], -1f, 1f);
                    samples[pos++] = sampleLeft;
                    samples[pos++] = sampleRight;
                }
                Debug.Log("Writted" + pos);
            }

            public void Dispose()
            {
                eventInstance?.stop(STOP_MODE.IMMEDIATE);
                eventInstance?.release();
                eventInstance = null;
            }
        }



        private interface IDecoder
        {
            void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right);
        }



        private class MusDecoder : IDecoder
        {
            public static readonly int SampleRate = 44100;
            public static readonly int BlockLength = SampleRate / 140;

            public static readonly byte[] MusHeader = new byte[]
            {
                (byte)'M',
                (byte)'U',
                (byte)'S',
                0x1A
            };

            private byte[] data;
            private bool loop;

            private int scoreLength;
            private int scoreStart;
            private int channelCount;
            private int channelCount2;
            private int instrumentCount;
            private int[] instruments;

            private MusEvent[] events;
            private int eventCount;

            private int[] lastVolume;
            private int p;
            private int delay;

            private int blockWrote;

            public MusDecoder(byte[] data, bool loop)
            {
                CheckHeader(data);

                this.data = data;
                this.loop = loop;

                scoreLength = BitConverter.ToUInt16(data, 4);
                scoreStart = BitConverter.ToUInt16(data, 6);
                channelCount = BitConverter.ToUInt16(data, 8);
                channelCount2 = BitConverter.ToUInt16(data, 10);
                instrumentCount = BitConverter.ToUInt16(data, 12);
                instruments = new int[instrumentCount];
                for (var i = 0; i < instruments.Length; i++)
                {
                    instruments[i] = BitConverter.ToUInt16(data, 16 + 2 * i);
                }

                events = new MusEvent[128];
                for (var i = 0; i < events.Length; i++)
                {
                    events[i] = new MusEvent();
                }
                eventCount = 0;

                lastVolume = new int[16];

                Reset();

                blockWrote = BlockLength;
            }

            private static void CheckHeader(byte[] data)
            {
                for (var p = 0; p < MusHeader.Length; p++)
                {
                    if (data[p] != MusHeader[p])
                    {
                        throw new Exception("Invalid format!");
                    }
                }
            }

            public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
            {
                var wrote = 0;
                while (wrote < left.Length)
                {
                    if (blockWrote == synthesizer.BlockSize)
                    {
                        ProcessMidiEvents(synthesizer);
                        blockWrote = 0;
                    }

                    var srcRem = synthesizer.BlockSize - blockWrote;
                    var dstRem = left.Length - wrote;
                    var rem = Math.Min(srcRem, dstRem);

                    synthesizer.Render(left.Slice(wrote, rem), right.Slice(wrote, rem));

                    blockWrote += rem;
                    wrote += rem;
                }
            }

            private void ProcessMidiEvents(Synthesizer synthesizer)
            {
                if (delay > 0)
                {
                    delay--;
                }

                if (delay == 0)
                {
                    delay = ReadSingleEventGroup();
                    SendEvents(synthesizer);

                    if (delay == -1)
                    {
                        synthesizer.NoteOffAll(false);

                        if (loop)
                        {
                            Reset();
                        }
                    }
                }
            }

            private void Reset()
            {
                for (var i = 0; i < lastVolume.Length; i++)
                {
                    lastVolume[i] = 0;
                }

                p = scoreStart;

                delay = 0;
            }

            private int ReadSingleEventGroup()
            {
                eventCount = 0;
                while (true)
                {
                    var result = ReadSingleEvent();
                    if (result == ReadResult.EndOfGroup)
                    {
                        break;
                    }
                    else if (result == ReadResult.EndOfFile)
                    {
                        return -1;
                    }
                }

                var time = 0;
                while (true)
                {
                    var value = data[p++];
                    time = time * 128 + (value & 127);
                    if ((value & 128) == 0)
                    {
                        break;
                    }
                }

                return time;
            }

            private ReadResult ReadSingleEvent()
            {
                var channelNumber = data[p] & 0xF;

                if (channelNumber == 15)
                {
                    channelNumber = 9;
                }
                else if (channelNumber >= 9)
                {
                    channelNumber++;
                }

                var eventType = (data[p] & 0x70) >> 4;
                var last = (data[p] >> 7) != 0;

                p++;

                var me = events[eventCount];
                eventCount++;

                switch (eventType)
                {
                    case 0: // RELEASE NOTE
                        me.Type = 0;
                        me.Channel = channelNumber;

                        var releaseNote = data[p++];

                        me.Data1 = releaseNote;
                        me.Data2 = 0;

                        break;

                    case 1: // PLAY NOTE
                        me.Type = 1;
                        me.Channel = channelNumber;

                        var playNote = data[p++];
                        var noteNumber = playNote & 127;
                        var noteVolume = (playNote & 128) != 0 ? data[p++] : -1;

                        me.Data1 = noteNumber;
                        if (noteVolume == -1)
                        {
                            me.Data2 = lastVolume[channelNumber];
                        }
                        else
                        {
                            me.Data2 = noteVolume;
                            lastVolume[channelNumber] = noteVolume;
                        }

                        break;

                    case 2: // PITCH WHEEL
                        me.Type = 2;
                        me.Channel = channelNumber;

                        var pitchWheel = data[p++];

                        var pw2 = (pitchWheel << 7) / 2;
                        var pw1 = pw2 & 127;
                        pw2 >>= 7;
                        me.Data1 = pw1;
                        me.Data2 = pw2;

                        break;

                    case 3: // SYSTEM EVENT
                        me.Type = 3;
                        me.Channel = channelNumber;

                        var systemEvent = data[p++];
                        me.Data1 = systemEvent;
                        me.Data2 = 0;

                        break;

                    case 4: // CONTROL CHANGE
                        me.Type = 4;
                        me.Channel = channelNumber;

                        var controllerNumber = data[p++];
                        var controllerValue = data[p++];

                        me.Data1 = controllerNumber;
                        me.Data2 = controllerValue;

                        break;

                    case 6: // END OF FILE
                        return ReadResult.EndOfFile;

                    default:
                        throw new Exception("Unknown event type!");
                }

                if (last)
                {
                    return ReadResult.EndOfGroup;
                }
                else
                {
                    return ReadResult.Ongoing;
                }
            }

            private void SendEvents(Synthesizer synthesizer)
            {
                for (var i = 0; i < eventCount; i++)
                {
                    var me = events[i];
                    switch (me.Type)
                    {
                        case 0: // RELEASE NOTE
                            synthesizer.NoteOff(me.Channel, me.Data1);
                            break;

                        case 1: // PLAY NOTE
                            synthesizer.NoteOn(me.Channel, me.Data1, me.Data2);
                            break;

                        case 2: // PITCH WHEEL
                            synthesizer.ProcessMidiMessage(me.Channel, 0xE0, me.Data1, me.Data2);
                            break;

                        case 3: // SYSTEM EVENT
                            switch (me.Data1)
                            {
                                case 11: // ALL NOTES OFF
                                    synthesizer.NoteOffAll(me.Channel, false);
                                    break;

                                case 14: // RESET ALL CONTROLS
                                    synthesizer.ResetAllControllers(me.Channel);
                                    break;
                            }
                            break;

                        case 4: // CONTROL CHANGE
                            switch (me.Data1)
                            {
                                case 0: // PROGRAM CHANGE
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xC0, me.Data2, 0);
                                    break;

                                case 1: // BANK SELECTION
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x00, me.Data2);
                                    break;

                                case 2: // MODULATION
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x01, me.Data2);
                                    break;

                                case 3: // VOLUME
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x07, me.Data2);
                                    break;

                                case 4: // PAN
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x0A, me.Data2);
                                    break;

                                case 5: // EXPRESSION
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x0B, me.Data2);
                                    break;

                                case 6: // REVERB
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x5B, me.Data2);
                                    break;

                                case 7: // CHORUS
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x5D, me.Data2);
                                    break;

                                case 8: // PEDAL
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x40, me.Data2);
                                    break;
                            }
                            break;
                    }
                }
            }

            private class MusEvent
            {
                public int Type;
                public int Channel;
                public int Data1;
                public int Data2;
            }

            private enum ReadResult
            {
                Ongoing,
                EndOfGroup,
                EndOfFile
            }
        }



        private class MidiDecoder : IDecoder
        {
            public static readonly byte[] MidiHeader = new byte[]
            {
                (byte)'M',
                (byte)'T',
                (byte)'h',
                (byte)'d'
            };

            private MidiFile midi;
            private MidiFileSequencer sequencer;

            private bool loop;

            public MidiDecoder(byte[] data, bool loop)
            {
                midi = new MidiFile(new MemoryStream(data));

                this.loop = loop;
            }

            public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
            {
                if (sequencer == null)
                {
                    sequencer = new MidiFileSequencer(synthesizer);
                    sequencer.Play(midi, loop);
                }

                sequencer.Render(left, right);
            }
        }
    }
}