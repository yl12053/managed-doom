using System;
using System.IO;
using ManagedDoom.Video;
using UnityEngine;
using Object = UnityEngine.Object;
using Renderer = ManagedDoom.Video.Renderer;

namespace ManagedDoom.Duckov
{
    public class DuckovVideo: IVideo, IDisposable
    {
        private Renderer renderer;
        
        private int textureWidth;
        private int textureHeight;
        
        public Texture2D Texture { get; private set; }

        private byte[] textureData;

        private int windowWidth;
        private int windowHeight;

        public static Func<bool> query = () => false;

        public DuckovVideo(Config config, GameContent content)
        {
            try
            {
                renderer = new Renderer(config, content);
                textureWidth = renderer.Width;
                textureHeight = renderer.Height;

                Texture = new Texture2D(renderer.Height, renderer.Width, TextureFormat.RGBA32, false);
                Texture.filterMode = FilterMode.Point;
                
                textureData = new byte[4 * renderer.Width * renderer.Height];
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                Dispose();
            }
        }

        public void Render(Doom doom, Fixed frameFrac)
        {
            renderer.Render(doom, textureData, frameFrac);
            Texture.LoadRawTextureData(textureData);
            Texture.Apply();
        }
        
        public void InitializeWipe()
        {
            renderer.InitializeWipe();
        }

        public bool HasFocus()
        {
            return query();
        }

        public void Dispose()
        {
            Debug.Log("Dispose");
            Object.Destroy(Texture);
            Texture = null;
        }
        
        public int WipeBandCount => renderer.WipeBandCount;
        public int WipeHeight => renderer.WipeHeight;

        public int MaxWindowSize => renderer.MaxWindowSize;

        public int WindowSize
        {
            get => renderer.WindowSize;
            set => renderer.WindowSize = value;
        }

        public bool DisplayMessage
        {
            get => renderer.DisplayMessage;
            set => renderer.DisplayMessage = value;
        }

        public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;

        public int GammaCorrectionLevel
        {
            get => renderer.GammaCorrectionLevel;
            set => renderer.GammaCorrectionLevel = value;
        }
    }
}