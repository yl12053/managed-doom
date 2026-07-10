//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//



using System;
using FeatherMod.Events;
using JetBrains.Annotations;
using ManagedDoom.Duckov;
using ManagedDoom.Event;
using ManagedDoom.Video;
using ModSetting.Api;
using UnityEngine;
using Renderer = ManagedDoom.Video.Renderer;

namespace ManagedDoom
{
    public sealed class Config
    {
        [CanBeNull] private SettingsBuilder settingBuilder;
        
        public KeyCode key_turnleft;
        public KeyCode key_turnright;
        public KeyCode key_run;
        public KeyCode key_strafe;

        private int _mouse_sensitivity;

        public int mouse_sensitivity
        {
            get => _mouse_sensitivity;
            set
            {
                _mouse_sensitivity = value;
                settingBuilder?.SetValue(nameof(mouse_sensitivity), value);
            }
        }
        public bool mouse_disableyaxis;

        public bool game_alwaysrun;

        public int video_screenwidth;
        public int video_screenheight;
        public bool video_highresolution;
        public bool video_displaymessage;
        public bool video_fullscreen;
        private int _video_gamescreensize;

        public int video_gamescreensize
        {
            get => _video_gamescreensize;
            set
            {
                _video_gamescreensize = value;
                settingBuilder?.SetValue(nameof(video_gamescreensize), value);
            }
        }
        public int video_gammacorrection;
        public int video_fpsscale;

        private int _audio_soundvolume;
        private int _audio_musicvolume;

        public int audio_soundvolume
        {
            get => _audio_soundvolume;
            set
            {
                _audio_soundvolume = value;
                settingBuilder?.SetValue(nameof(audio_soundvolume), value);
            }
        }
        public int audio_musicvolume
        {
            get => _audio_musicvolume;
            set
            {
                _audio_musicvolume = value;
                settingBuilder?.SetValue(nameof(audio_musicvolume), value);
            }
        }
        public bool audio_randompitch;
        public string audio_soundfont;
        public bool audio_musiceffect;

        private bool isRestoredFromFile;

        // Default settings.
        public Config()
        {
            key_turnleft = KeyCode.LeftArrow;
            key_turnright = KeyCode.RightArrow;
            key_run = KeyCode.LeftShift;
            key_strafe = KeyCode.LeftAlt;

            _mouse_sensitivity = 8;
            mouse_disableyaxis = false;

            game_alwaysrun = true;

            video_screenwidth = 640;
            video_screenheight = 400;
            video_fullscreen = false;
            video_highresolution = true;
            _video_gamescreensize = 7;
            video_displaymessage = true;
            video_gammacorrection = 2;
            video_fpsscale = 2;

            _audio_soundvolume = 8;
            _audio_musicvolume = 8;
            audio_randompitch = true;
            audio_soundfont = "TimGM6mb.sf2";
            audio_musiceffect = true;

            isRestoredFromFile = false;
        }

        public Config(SettingsBuilder settingsBuilder) : this()
        {
            settingBuilder = settingsBuilder;
            try
            {
                Debug.Log("Restore settings: ");

                if (settingsBuilder.HasConfig())
                {
                    key_turnleft = settingsBuilder.GetSavedValue("key_turnleft", out KeyCode k1) ? k1 : key_turnleft;
                    key_turnright = settingsBuilder.GetSavedValue("key_turnright", out KeyCode k2) ? k2 : key_turnright;
                    key_run = settingsBuilder.GetSavedValue("key_run", out KeyCode k3) ? k3 : key_run;
                    key_strafe = settingsBuilder.GetSavedValue("key_strafe", out KeyCode k4) ? k4 : key_strafe;
                    mouse_sensitivity = settingsBuilder.GetSavedValue("mouse_sensitivity", out int i1)
                        ? i1
                        : mouse_sensitivity;
                    mouse_disableyaxis = settingsBuilder.GetSavedValue("mouse_disableyaxis", out bool b1)
                        ? b1
                        : mouse_disableyaxis;
                    
                    if (settingsBuilder.GetSavedValue(nameof(game_alwaysrun), out bool tempAlwaysRun)) game_alwaysrun = tempAlwaysRun;
                    
                    if (settingsBuilder.GetSavedValue(nameof(video_highresolution), out bool tempHighRes)) video_highresolution = tempHighRes;
                    
                    if (settingsBuilder.GetSavedValue(nameof(video_gamescreensize), out int tempScreenSize)) _video_gamescreensize = tempScreenSize;
                    if (settingsBuilder.GetSavedValue(nameof(video_gammacorrection), out int tempGamma)) video_gammacorrection = tempGamma;
                    if (settingsBuilder.GetSavedValue(nameof(video_fpsscale), out int tempFpsScale)) video_fpsscale = tempFpsScale;
                    
                    if (settingsBuilder.GetSavedValue(nameof(audio_soundvolume), out int tempSndVol)) _audio_soundvolume = tempSndVol;
                    if (settingsBuilder.GetSavedValue(nameof(audio_musicvolume), out int tempMusVol)) _audio_musicvolume = tempMusVol;
                    if (settingsBuilder.GetSavedValue(nameof(audio_randompitch), out bool tempPitch)) audio_randompitch = tempPitch;
                    if (settingsBuilder.GetSavedValue(nameof(audio_musiceffect), out bool tempMusEff)) audio_musiceffect = tempMusEff;
                    
                    isRestoredFromFile = true;
                }

                settingsBuilder
                    .AddKeybinding("key_turnleft", getName("key.doom.turn_left"), key_turnleft, KeyCode.LeftArrow,
                        (v) => key_turnleft = v)
                    .AddKeybinding("key_turnright", getName("key.doom.turn_right"), key_turnright, KeyCode.RightArrow,
                        (v) => key_turnright = v)
                    .AddKeybinding("key_run", getName("key.doom.run"), key_run, KeyCode.LeftShift, (v) => key_run = v)
                    .AddKeybinding("key_strafe", getName("key.doom.strafe"), key_strafe, KeyCode.LeftAlt,
                        (v) => key_strafe = v)
                    .AddSlider("mouse_sensitivity", getName("mouse.doom.sensitivity"), mouse_sensitivity, 1, 15,
                        v => _mouse_sensitivity = v, 2)
                    .AddToggle("mouse_disableyaxis", getName("mouse.doom.disableyaxis"), mouse_disableyaxis,
                        v => mouse_disableyaxis = v)
                    .AddToggle(nameof(game_alwaysrun), getName("game.doom.alwaysrun"), game_alwaysrun,
                        v => game_alwaysrun = v)
                    .AddToggle(nameof(video_highresolution), getName("video.doom.highresolution"), video_highresolution,
                        v => video_highresolution = v)
                    .AddSlider(nameof(video_gamescreensize), getName("video.doom.gamescreensize"), video_gamescreensize,
                        0, ThreeDRenderer.MaxScreenSize, v =>
                        {
                            _video_gamescreensize = v;
                            EventBusManager.Instance.Sync.Post(new VideoGameScreenSizeChangeEvent());
                        }, 1)
                    .AddSlider(nameof(video_gammacorrection), getName("video.doom.gammacorrection"),
                        video_gammacorrection, 0, Renderer.MaxGammaCorrectionLevelStatic,
                        v => video_gammacorrection = v, 10)
                    .AddSlider(nameof(video_fpsscale), getName("video.doom.fpsscale"), video_fpsscale, 0, 100,
                        v => video_fpsscale = v, 3)
                    .AddSlider(nameof(audio_soundvolume), getName("audio.doom.soundvolume"), audio_soundvolume, 0, 15,
                        v => _audio_soundvolume = v, 2)
                    .AddSlider(nameof(audio_musicvolume), getName("audio.doom.musicvolume"), audio_musicvolume, 0, 15,
                        v => _audio_musicvolume = v, 2)
                    .AddToggle(nameof(audio_randompitch), getName("audio.doom.randompitch"), audio_randompitch,
                        v => audio_randompitch = v)
                    .AddToggle(nameof(audio_musiceffect), getName("audio.doom.musiceffect"), audio_musiceffect,
                        v => audio_musiceffect = v)
                    .AddButton("reset_all", getName("settings.doom.reset"), getName("settings.doom.reset"), () =>
                    {
                        var defaults = new Config();
                        key_turnleft = defaults.key_turnleft;
                        key_turnright = defaults.key_turnright;
                        key_run = defaults.key_run;
                        key_strafe = defaults.key_strafe;
                        _mouse_sensitivity = defaults._mouse_sensitivity;
                        mouse_disableyaxis = defaults.mouse_disableyaxis;
                        game_alwaysrun = defaults.game_alwaysrun;
                        video_highresolution = defaults.video_highresolution;
                        _video_gamescreensize = defaults._video_gamescreensize;
                        video_gammacorrection = defaults.video_gammacorrection;
                        video_fpsscale = defaults.video_fpsscale;
                        _audio_soundvolume = defaults._audio_soundvolume;
                        _audio_musicvolume = defaults._audio_musicvolume;
                        audio_randompitch = defaults.audio_randompitch;
                        audio_musiceffect = defaults.audio_musiceffect;
                        settingsBuilder.SetValue("key_turnleft", key_turnleft);
                        settingsBuilder.SetValue("key_turnright", key_turnright);
                        settingsBuilder.SetValue("key_run", key_run);
                        settingsBuilder.SetValue("key_strafe", key_strafe);
                        settingsBuilder.SetValue("mouse_sensitivity", mouse_sensitivity);
                        settingsBuilder.SetValue("mouse_disableyaxis", mouse_disableyaxis);
                        settingsBuilder.SetValue(nameof(game_alwaysrun), game_alwaysrun);
                        settingsBuilder.SetValue(nameof(video_highresolution), video_highresolution);
                        settingsBuilder.SetValue(nameof(video_gamescreensize), video_gamescreensize);
                        settingsBuilder.SetValue(nameof(video_gammacorrection), video_gammacorrection);
                        settingsBuilder.SetValue(nameof(video_fpsscale), video_fpsscale);
                        settingsBuilder.SetValue(nameof(audio_soundvolume), audio_soundvolume);
                        settingsBuilder.SetValue(nameof(audio_musicvolume), audio_musicvolume);
                        settingsBuilder.SetValue(nameof(audio_randompitch), audio_randompitch);
                        settingsBuilder.SetValue(nameof(audio_musiceffect), audio_musiceffect);
                        EventBusManager.Instance.Sync.Post(new VideoGameScreenSizeChangeEvent());
                    });

                Debug.Log("OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public static string getName(string raw)
        {
            return SodaCraft.Localizations.LocalizationManager.TryGetOverrideText(raw, out string name) ? name : raw;
        }

        public bool IsRestoredFromFile => isRestoredFromFile;
    }
}
