using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using FMOD;
using FMODUnity;
using ManagedDoom.Audio;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ManagedDoom.Duckov;

public class DuckovSound: ISound, IDisposable
{
    private static readonly int channelCount = 8;

    private static readonly float fastDecay = (float)Math.Pow(0.5, 1.0 / (35 / 5));
    private static readonly float slowDecay = (float)Math.Pow(0.5, 1.0 / 35);

    private static readonly float clipDist = 1200;
    private static readonly float closeDist = 160;
    private static readonly float attenuator = clipDist - closeDist;

    private Config config;

    private byte[][] buffers;
    private int[] bufferSampleRate;
    private float[] amplitudes;
    private bool bufferInited = false;
    
    private DoomRandom random;
    
    private Sfx uiReserved;
    
    public class PackedAudio
    {
        public Sound? sound;
        public Channel? channel;
        public Vector3 vec = Vector3.zero;

        public void UpdateLocation() 
        {
            if (channel != null) 
            {
                VECTOR vecdest = VectorCalculation(vec);
                var emp = new VECTOR()
                {
                    x = 0,
                    y = 0,
                    z = 0
                };
                channel.Value.set3DAttributes(
                    ref vecdest,
                    ref emp
                );
            }
        }
        
        public bool isPlaying
        {
            get
            {
                if (channel == null) return false;
                if (channel.Value.isPlaying(out var res) == RESULT.OK) return res;
                return false;
            }
        }

        public bool isPaused()
        {
            if (channel == null) return false;
            if (channel.Value.getPaused(out var paused) == RESULT.OK) return paused;
            return false;
        }

        public void doPause(bool pause)
        {
            if (channel == null) return;
            var res = channel.Value.setPaused(pause);
            if (res != RESULT.OK) Debug.LogError(res);
        }

        public bool isEnded()
        {
            if (channel == null) return true;
            if (channel.Value.isPlaying(out var playing) == RESULT.OK) return !playing;
            return true;
        }

        public void Stop()
        {
            channel?.stop();
            channel = null;
            sound?.release();
            sound = null;
        }
    }
    
    private PackedAudio? uiEvent = null;

    private Mobj listener;

    private float masterVolumeDecay;

    private DateTime lastUpdate;

    private PackedAudio[] channels;
    private ChannelInfo[] infos;

    private ChannelGroup? group = null;
    
    public bool Disposed { get; private set; }

    public float VolumeMultiply;
    
    public DuckovSound(Config config, GameContent content, float mul)
    {
        Disposed = false;
        VolumeMultiply = mul;
        try
        {
            Debug.Log("Initialize sound: ");

            var res = RuntimeManager.CoreSystem.createChannelGroup(null, out var groups);
            group = groups;
            if (res != RESULT.OK)
            {
                throw new SystemNotInitializedException(res, "");
            }
            var bus = RuntimeManager.GetBus("bus:/Master/SFX");
            bus.getChannelGroup(out var channelGroup);
            channelGroup.addGroup(groups, true);
            
            this.config = config;

            config.audio_soundvolume = Math.Clamp(config.audio_soundvolume, 0, MaxVolume);

            buffers = new byte[DoomInfo.SfxNames.Length][];
            amplitudes = new float[DoomInfo.SfxNames.Length];
            bufferSampleRate = new int[DoomInfo.SfxNames.Length];

            if (config.audio_randompitch)
            {
                random = new DoomRandom();
            }

            for (var i = 0; i < DoomInfo.SfxNames.Length; i++)
            {
                var name = "DS" + DoomInfo.SfxNames[i].ToString().ToUpper();
                var lump = content.Wad.GetLumpNumber(name);
                if (lump == -1)
                {
                    continue;
                }

                int sampleRate;
                int sampleCount;
                Span<byte> samples = GetSamples(content.Wad, name, out sampleRate, out sampleCount);
                if (!samples.IsEmpty)
                {
                    byte[] heap = new byte[samples.Length];
                    samples.CopyTo(heap);
                    buffers[i] = heap;
                    amplitudes[i] = GetAmplitude(samples, sampleRate, sampleCount);
                    bufferSampleRate[i] = sampleRate;
                }
                else
                {
                    buffers[i] = null;
                    amplitudes[i] = -1;
                    bufferSampleRate[i] = -1;
                }
            }
            
            channels = new PackedAudio[channelCount];
            infos = new ChannelInfo[channelCount];
            for (var i = 0; i < channels.Length; i++)
            {
                channels[i] = null;
                infos[i] = new ChannelInfo();
            }

            uiEvent = null;
            uiReserved = Sfx.NONE;
            
            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;

            lastUpdate = DateTime.MinValue;

            Debug.Log("OK");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            Dispose();
        }
    }
    
    private static Span<byte> GetSamples(Wad wad, string name, out int sampleRate, out int sampleCount)
    {
        var data = wad.ReadLump(name);

        if (data.Length < 8)
        {
            sampleRate = -1;
            sampleCount = -1;
            return null;
        }

        sampleRate = BitConverter.ToUInt16(data, 2);
        sampleCount = BitConverter.ToInt32(data, 4);

        var offset = 8;
        if (ContainsDmxPadding(data))
        {
            offset += 16;
            sampleCount -= 32;
        }

        if (sampleCount > 0)
        {
            return data.AsSpan(offset, sampleCount);
        }
        else
        {
            return Span<byte>.Empty;
        }
    }
    
    private static bool ContainsDmxPadding(byte[] data)
    {
        var sampleCount = BitConverter.ToInt32(data, 4);
        if (sampleCount < 32)
        {
            return false;
        }
        else
        {
            var first = data[8];
            for (var i = 1; i < 16; i++)
            {
                if (data[8 + i] != first)
                {
                    return false;
                }
            }

            var last = data[8 + sampleCount - 1];
            for (var i = 1; i < 16; i++)
            {
                if (data[8 + sampleCount - i - 1] != last)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static float GetAmplitude(Span<byte> samples, int sampleRate, int sampleCount)
    {
        var max = 0;
        if (sampleCount > 0)
        {
            var count = Math.Min(sampleRate / 5, sampleCount);
            for (var t = 0; t < count; t++)
            {
                var a = samples[t] - 128;
                if (a < 0)
                {
                    a = -a;
                }
                if (a > max)
                {
                    max = a;
                }
            }
        }
        return (float)max / 128;
    }

    public void SetListener(Mobj listener)
    {
        this.listener = listener;
    }
    
    public void Update()
    {
            if (Disposed) return;
            if (group != null)
            {
                group.Value.setVolume(VolumeMultiply);
            }
            var now = DateTime.Now;
            if ((now - lastUpdate).TotalSeconds < 0.01)
            {
                // Don't update so frequently (for timedemo).
                return;
            }

            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                var channel = channels[i];

                if (info.Playing != Sfx.NONE)
                {
                    if (channel != null && !channel.isEnded())
                    {
                        if (info.Type == SfxType.Diffuse)
                        {
                            info.Priority *= slowDecay;
                        }
                        else
                        {
                            info.Priority *= fastDecay;
                        }

                        SetParam(channel, info);
                    }
                    else
                    {
                        info.Playing = Sfx.NONE;
                        if (info.Reserved == Sfx.NONE)
                        {
                            info.Source = null;
                        }

                        channel?.Stop();
                        channel = null;
                    }
                }

                if (info.Reserved != Sfx.NONE)
                {
                    if (info.Playing != Sfx.NONE)
                    {
                        channel?.Stop();
                    }

                    int index = (int)info.Reserved;
                    var newchan = MakePlay(buffers[index], bufferSampleRate[index], 1f,
                        GetPitch(info.Type, info.Reserved),
                        new Vector3(0, 0, 1), true);
                    SetParam(newchan, info);
                    newchan?.doPause(false);
                    channels[i] = newchan;

                    info.Playing = info.Reserved;
                    info.Reserved = Sfx.NONE;
                }
                
                channels[i]?.UpdateLocation();
            }

            if (uiReserved != Sfx.NONE)
            {
                if (uiEvent != null)
                {
                    if (uiEvent.isPlaying)
                    {
                        uiEvent.Stop();
                        uiEvent = null;
                    }
                }

                int index = (int)uiReserved;
                if (buffers[index] == null)
                {
                    uiEvent = null;
                }
                else
                {
                    uiEvent = MakePlay(buffers[index], bufferSampleRate[index], masterVolumeDecay,
                        1f, new Vector3(0, 0, -1));
                }

                uiReserved = Sfx.NONE;
            }
            uiEvent?.UpdateLocation();

            lastUpdate = now;
    }
    
    public static VECTOR VectorCalculation(Vector3 destination)
    {
        RuntimeManager.Instance.studioSystem.getListenerAttributes(0, out var attr);
        Vector3 pos = new Vector3(attr.position.x, attr.position.y, attr.position.z);
        Vector3 forward = new Vector3(attr.forward.x, attr.forward.y, attr.forward.z).normalized;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        Vector3 localPos = pos + rotation * destination;
        return localPos.ToFMODVector();
    }

    public PackedAudio? MakePlay(byte[] sample, int sampleRate, float vol, float pitch, Vector3? pos = null, bool paused = false)
    {
            if (Disposed) return null;
            if (group == null) return null;
            CREATESOUNDEXINFO exinfo = new()
            {
                cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
                format = SOUND_FORMAT.PCM8,
                defaultfrequency = sampleRate,
                decodebuffersize = (uint) sample.Length,
                length = (uint) sample.Length,
                numchannels = 1,
            };
            GCHandle handle = GCHandle.Alloc(sample, GCHandleType.Pinned);
            RuntimeManager.CoreSystem.createSound(handle.AddrOfPinnedObject(), (pos == null ? MODE._2D : MODE._3D) | MODE.OPENMEMORY | MODE.OPENRAW, ref exinfo,
                out var sound);
            // handle.Free();
            var result = RuntimeManager.CoreSystem.playSound(sound, group.Value, true, out var channel);
            if (result == RESULT.OK)
            {
                if (pos != null)
                {
                    var vec = VectorCalculation(pos.Value);
                    var emp = new VECTOR()
                    {
                        x = 0,
                        y = 0,
                        z = 0
                    };
                    channel.set3DAttributes(
                        ref vec,
                        ref emp
                    );
                }

                channel.setVolume(vol);
                channel.setPitch(pitch);
                var res2 = paused ? RESULT.OK : channel.setPaused(false);
                if (res2 == RESULT.OK)
                {
                    return new PackedAudio()
                    {
                        channel = channel,
                        sound = sound,
                        vec = pos.GetValueOrDefault(Vector3.zero)
                    };
                }
                Debug.LogError("2" + res2);
                channel.stop();
            }
            else
            {
                Debug.LogError("1" + result);
            }
            sound.release();
            return null;
    }
    
    public void StartSound(Sfx sfx)
    {
        if (buffers[(int)sfx] == null)
        {
            return;
        }

        uiReserved = sfx;
    }

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type)
    {
        StartSound(mobj, sfx, type, 100);
    }
    
    public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume)
    {
        if (buffers[(int)sfx] == null)
        {
            return;
        }

        var x = (mobj.X - listener.X).ToFloat();
        var y = (mobj.Y - listener.Y).ToFloat();
        var dist = MathF.Sqrt(x * x + y * y);

        float priority;
        if (type == SfxType.Diffuse)
        {
            priority = volume;
        }
        else
        {
            priority = amplitudes[(int)sfx] * GetDistanceDecay(dist) * volume;
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Source == mobj && info.Type == type)
            {
                info.Reserved = sfx;
                info.Priority = priority;
                info.Volume = volume;
                return;
            }
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Reserved == Sfx.NONE && info.Playing == Sfx.NONE)
            {
                info.Reserved = sfx;
                info.Priority = priority;
                info.Source = mobj;
                info.Type = type;
                info.Volume = volume;
                return;
            }
        }

        var minPriority = float.MaxValue;
        var minChannel = -1;
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Priority < minPriority)
            {
                minPriority = info.Priority;
                minChannel = i;
            }
        }
        if (priority >= minPriority)
        {
            var info = infos[minChannel];
            info.Reserved = sfx;
            info.Priority = priority;
            info.Source = mobj;
            info.Type = type;
            info.Volume = volume;
        }
    }
    
    public void StopSound(Mobj mobj)
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Source == mobj)
            {
                info.LastX = info.Source.X;
                info.LastY = info.Source.Y;
                info.Source = null;
                info.Volume /= 5;
            }
        }
    }
    
    public void Reset()
    {
        if (random != null)
        {
            random.Clear();
        }

        for (var i = 0; i < infos.Length; i++)
        {
            channels[i]?.Stop();
            channels[i] = null;
            infos[i].Clear();
        }

        listener = null;
    }
    
    public void Pause()
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var channel = channels[i];
            if (channel == null) return;

            if (!channel.isEnded())
            {
                if (channel.channel == null) return;
                if (channel.sound == null) return;
                channel.channel.Value.getPosition(out uint ms, TIMEUNIT.MS);
                channel.sound.Value.getLength(out uint length, TIMEUNIT.MS);
                if (length - ms >= 200)
                {
                    channels[i].doPause(true);
                }
            }
        }
    }
    
    public void Resume()
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var channel = channels[i];

            if (channel != null && channel.isPaused())
            {
                channel.doPause(false);
            }
        }
    }
    
    private void SetParam(PackedAudio sound, ChannelInfo info)
    {
        if (sound == null) return;
        if (sound.channel == null) return;
        if (info.Type == SfxType.Diffuse)
        {
            sound.vec = new Vector3(0, 0, -1);
            sound.channel.Value.setVolume(0.01F * masterVolumeDecay * info.Volume);
        }
        else
        {
            Fixed sourceX;
            Fixed sourceY;
            if (info.Source == null)
            {
                sourceX = info.LastX;
                sourceY = info.LastY;
            }
            else
            {
                sourceX = info.Source.X;
                sourceY = info.Source.Y;
            }

            var x = (sourceX - listener.X).ToFloat();
            var y = (sourceY - listener.Y).ToFloat();

            if (Math.Abs(x) < 16 && Math.Abs(y) < 16)
            {
                sound.vec = new Vector3(0, 0, -1);
                sound.channel.Value.setVolume(0.01F * masterVolumeDecay * info.Volume);
            }
            else
            {
                var dist = MathF.Sqrt(x * x + y * y);
                var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();
                sound.vec = new Vector3(-MathF.Sin(angle), 0, -MathF.Cos(angle));
                sound.channel.Value.setVolume( 0.01F * masterVolumeDecay * GetDistanceDecay(dist) * info.Volume);
            }
        }
    }
    
    private float GetDistanceDecay(float dist)
    {
        if (dist < closeDist)
        {
            return 1F;
        }
        else
        {
            return Math.Max((clipDist - dist) / attenuator, 0F);
        }
    }

    private float GetPitch(SfxType type, Sfx sfx)
    {
        if (random != null)
        {
            if (sfx == Sfx.ITEMUP || sfx == Sfx.TINK || sfx == Sfx.RADIO)
            {
                return 1.0F;
            }
            else if (type == SfxType.Voice)
            {
                return 1.0F + 0.075F * (random.Next() - 128) / 128;
            }
            else
            {
                return 1.0F + 0.025F * (random.Next() - 128) / 128;
            }
        }
        else
        {
            return 1.0F;
        }
    }

    public void Dispose()
    {
            Disposed = true;
            uiEvent?.Stop();
            uiEvent = null;

            for (int i = 0; i < channelCount; i++)
            {
                channels[i]?.Stop();
                channels[i] = null;
            }

            var groups = group;
            group = null;
            if (groups == null) return;
            groups.Value.stop();
            groups.Value.release();
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
            return config.audio_soundvolume;
        }

        set
        {
            config.audio_soundvolume = value;
            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
        }
    }
    
    private class ChannelInfo
    {
        public Sfx Reserved;
        public Sfx Playing;
        public float Priority;

        public Mobj Source;
        public SfxType Type;
        public int Volume;
        public Fixed LastX;
        public Fixed LastY;

        public void Clear()
        {
            Reserved = Sfx.NONE;
            Playing = Sfx.NONE;
            Priority = 0;
            Source = null;
            Type = 0;
            Volume = 0;
            LastX = Fixed.Zero;
            LastY = Fixed.Zero;
        }
    }

}