using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Duckov.MiniGames;
using UnityEngine;

namespace ManagedDoom.Duckov
{
    public partial class DuckovDoom: IDisposable
    {
        private CommandLineArgs args;

        private Config config;
        private GameContent content;

        public DuckovVideo video { get; private set; }
        private DuckovUserInput userInput;
        public DuckovMusic music;
        public DuckovSound sound;

        private Doom doom;
        private int fpsScale;
        private int frameCount;

        private Exception exception;
        private MiniGame mini;

        private string wadName;
        
        public bool Disposed { get; private set; }

        public DuckovDoom(CommandLineArgs args, Config config, MiniGame mini, string wadName)
        {
            Disposed = false;
            try
            {
                this.args = args;
                
                content = new GameContent(args);

                config.video_screenwidth = Math.Clamp(config.video_screenwidth, 320, 3200);
                config.video_screenheight = Math.Clamp(config.video_screenheight, 200, 2000);
                this.config = config;
                this.mini = mini;
                this.wadName = wadName;
            }
            catch (Exception e)
            {
                Dispose();
                Debug.LogError(e);
            }
        }
        
        private void Quit()
        {
            if (exception != null)
            {
                Debug.LogError(exception);
            }
            Close();
        }
        
        private void OnLoad(float mul, string namespaces)
        {
            video = new DuckovVideo(config, content);

            if (!args.nosound.Present && !(args.nosfx.Present && args.nomusic.Present))
            {
                if (!args.nosfx.Present)
                {
                    sound = new DuckovSound(config, content, mul);
                }
                if (!args.nomusic.Present)
                {
                    music = new DuckovMusic(config, content, Path.Combine(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), 
                        config.audio_soundfont),
                        mul);
                }
            }

            userInput = new DuckovUserInput(config, this, !args.nomouse.Present, mini);
            
            doom = new Doom(args, config, content, video, sound, music, userInput, wadName, namespaces);

            fpsScale = args.timedemo.Present ? 1 : config.video_fpsscale;
            frameCount = -1;
        }
        
        private void OnUpdate()
        {
            try
            {
                frameCount++;

                if (frameCount % fpsScale == 0)
                {
                    if (doom.Update() == UpdateResult.Completed)
                    {
                        Close();
                    }
                }
            }
            catch (Exception e)
            {
                exception = e;
            }

            if (exception != null)
            {
                Debug.LogError(exception);
                Close();
            }
        }

        private void Close()
        {
            mini.Console.StopInteract();
            Dispose();
        }
        
        private void OnRender()
        {
            try
            {
                var frameFrac = Fixed.FromInt(frameCount % fpsScale + 1) / fpsScale;
                video.Render(doom, frameFrac);
            }
            catch (Exception e)
            {
                exception = e;
            }
        }
        
        public void KeyDown(KeyCode key)
        {
            if (!(video?.HasFocus() ?? false)) return;
            doom.PostEvent(new DoomEvent(EventType.KeyDown, DuckovUserInput.CodeToDoom(key)));
        }

        public void KeyUp(KeyCode key)
        {
            if (!(video?.HasFocus() ?? false)) return;
            doom.PostEvent(new DoomEvent(EventType.KeyUp, DuckovUserInput.CodeToDoom(key)));
        }

        public void Dispose()
        {
            userInput?.Dispose();
            music?.Dispose();
            // sound?.Dispose();
            video?.Dispose();
            doom.EndGame();
            Disposed = true;
        }
        
        public string QuitMessage => doom.QuitMessage;
        public Exception Exception => exception;
    }
}