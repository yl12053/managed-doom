using System;

namespace ManagedDoom.Duckov
{
    public partial class DuckovDoom: IDisposable
    {
        private double timespan;
        private double perfps;

        public void DoStart(float mul = 1f)
        {
            config.video_fpsscale = Math.Clamp(config.video_fpsscale, 1, 100);
            var targetFps = 35 * config.video_fpsscale;
            perfps = 1d / targetFps;
            timespan = 0;
            OnLoad(mul);
            OnUpdate();
        }
        
        public void DoUpdate(double timeDelta)
        {
            timespan += timeDelta;
            while (timespan >= perfps)
            {
                OnUpdate();
                timespan -= perfps;
            }
            OnRender();
        }
    }
}