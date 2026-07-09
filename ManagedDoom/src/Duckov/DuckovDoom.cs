using System;
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

        private Doom doom;
        private int fpsScale;
        private int frameCount;

        private Exception exception;
        private MiniGame mini;

        public DuckovDoom(CommandLineArgs args, Config config, MiniGame mini)
        {
            try
            {
                this.args = args;
                
                content = new GameContent(args);

                config.video_screenwidth = Math.Clamp(config.video_screenwidth, 320, 3200);
                config.video_screenheight = Math.Clamp(config.video_screenheight, 200, 2000);
                this.config = config;
                this.mini = mini;
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
                ExceptionDispatchInfo.Throw(exception);
            }
        }
        
        private void OnLoad()
        {
            video = new DuckovVideo(config, content);

            /* if (!args.nosound.Present && !(args.nosfx.Present && args.nomusic.Present))
            {
                audioDevice = new AudioDevice();
                if (!args.nosfx.Present)
                {
                    sound = new SilkSound(config, content, audioDevice);
                }
                if (!args.nomusic.Present)
                {
                    music = SilkConfigUtilities.GetMusicInstance(config, content, audioDevice);
                }
            } */

            userInput = new DuckovUserInput(config, this, !args.nomouse.Present, mini);

            // doom = new Doom(args, config, content, video, sound, music, userInput);
            doom = new Doom(args, config, content, video, null, null, userInput);

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
                Close();
            }
        }

        private void Close()
        {
            // todo: knock player out of FC
            Dispose();
        }
        
        private void OnRender()
        {
            Debug.Log("Try render");
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
            // music?.Dispose();
            // sound?.Dispose();
            video?.Dispose();
            doom.EndGame();
        }
        
        public string QuitMessage => doom.QuitMessage;
        public Exception Exception => exception;
    }
}