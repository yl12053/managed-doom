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
using ModSetting.Api;
using UnityEngine;

namespace ManagedDoom
{
    public sealed class Config
    {
        public KeyCode key_turnleft;
        public KeyCode key_turnright;
        public KeyCode key_run;
        public KeyCode key_strafe;

        public int mouse_sensitivity;
        public bool mouse_disableyaxis;

        public bool game_alwaysrun;

        public int video_screenwidth;
        public int video_screenheight;
        public bool video_fullscreen;
        public bool video_highresolution;
        public bool video_displaymessage;
        public int video_gamescreensize;
        public int video_gammacorrection;
        public int video_fpsscale;

        public int audio_soundvolume;
        public int audio_musicvolume;
        public bool audio_randompitch;
        public string audio_soundfont;
        public bool audio_musiceffect;

        private bool isRestoredFromFile;

        // Default settings.
        public Config()
        {
            key_run = KeyCode.LeftShift;
            key_strafe = KeyCode.LeftAlt;

            mouse_sensitivity = 8;
            mouse_disableyaxis = false;

            game_alwaysrun = true;

            video_screenwidth = 640;
            video_screenheight = 400;
            video_fullscreen = false;
            video_highresolution = true;
            video_gamescreensize = 7;
            video_displaymessage = true;
            video_gammacorrection = 2;
            video_fpsscale = 2;

            audio_soundvolume = 8;
            audio_musicvolume = 8;
            audio_randompitch = true;
            audio_soundfont = "TimGM6mb.sf2";
            audio_musiceffect = true;

            isRestoredFromFile = false;
        }

        public Config(SettingsBuilder settingsBuilder) : this()
        {
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
                    
                    if (settingsBuilder.GetSavedValue(nameof(video_screenwidth), out int tempWidth)) video_screenwidth = tempWidth;
                    if (settingsBuilder.GetSavedValue(nameof(video_screenheight), out int tempHeight)) video_screenheight = tempHeight;
                    if (settingsBuilder.GetSavedValue(nameof(video_fullscreen), out bool tempFullscreen)) video_fullscreen = tempFullscreen;
                    if (settingsBuilder.GetSavedValue(nameof(video_highresolution), out bool tempHighRes)) video_highresolution = tempHighRes;
                    if (settingsBuilder.GetSavedValue(nameof(video_displaymessage), out bool tempMsg)) video_displaymessage = tempMsg;
                    if (settingsBuilder.GetSavedValue(nameof(video_gamescreensize), out int tempScreenSize)) video_gamescreensize = tempScreenSize;
                    if (settingsBuilder.GetSavedValue(nameof(video_gammacorrection), out int tempGamma)) video_gammacorrection = tempGamma;
                    if (settingsBuilder.GetSavedValue(nameof(video_fpsscale), out int tempFpsScale)) video_fpsscale = tempFpsScale;
                    
                    if (settingsBuilder.GetSavedValue(nameof(audio_soundvolume), out int tempSndVol)) audio_soundvolume = tempSndVol;
                    if (settingsBuilder.GetSavedValue(nameof(audio_musicvolume), out int tempMusVol)) audio_musicvolume = tempMusVol;
                    if (settingsBuilder.GetSavedValue(nameof(audio_randompitch), out bool tempPitch)) audio_randompitch = tempPitch;
                    if (settingsBuilder.GetSavedValue(nameof(audio_soundfont), out string tempSndFont)) audio_soundfont = tempSndFont;
                    if (settingsBuilder.GetSavedValue(nameof(audio_musiceffect), out bool tempMusEff)) audio_musiceffect = tempMusEff;
                    
                    isRestoredFromFile = true;
                }

                Debug.Log("OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public bool IsRestoredFromFile => isRestoredFromFile;
    }
}
