using ParticleEngine;
using ProtoEngine;
using ProtoEngine.Rendering;
using ProtoEngine.UI;
using SFML.Graphics;

var app = new ParticleEngineApp("Particle Engine", new Color(20, 20, 20), true);
app.Run();

public class ParticleEngineApp : Application
{
    public ParticleSystem particleSystem;

    public ParticleEngineApp(string name, Color windowFill, bool fullscreen) : base(name, windowFill, fullscreen)
    {
    }

    protected override void Setup()
    {
        base.Setup();
        
        particleSystem = new ParticleSystem(1000000,  0.5f, new(1000, 1000), true);

        window.Style.fontSize = window.Width / 1920f * 16f;
        window.ScaleRenderTexture(1920f / window.Width);

        var panelStyle = new Style()
        {
            width = "20em",
        };

        var statsPanel = new Panel(window);
        statsPanel.SetBaseStyle(panelStyle);
        new TextElement(statsPanel, "Stats", "2em", Alignment.Center);
        new LabeledElement<TextElement>(statsPanel, "Num Particles", new TextElement(() => particleSystem.Count.ToString("N0")));
        new LabeledElement<TextElement>(statsPanel, "Particle Capacity", new TextElement(() => particleSystem.Capacity.ToString("N0")));
        new LabeledElement<TextElement>(statsPanel, "Update FPS", new TextElement(() => updateLoop.measuredFPS.ToString("N0")));
        new LabeledElement<TextElement>(statsPanel, "Draw FPS", new TextElement(() => drawLoop.measuredFPS.ToString("N0")));
        new LabeledElement<TextElement>(statsPanel, "Fixed Update FPS", new TextElement(() => fixedUpdateLoop.measuredFPS.ToString("N0")));

        statsPanel.BuildBox();

        var controlsPanel = new Panel(window);
        controlsPanel.container.Style.top = "20em";
        controlsPanel.SetBaseStyle(panelStyle);
        new TextElement(controlsPanel, "Controls", "2em", Alignment.Center);

        var gravitySlider = new Slider(0, 0, 10, 0.1f);
        new LabeledElement<Slider>(controlsPanel, "Gravity", gravitySlider);
        gravitySlider.inputEvents.OnChange += (slider, value) => particleSystem.gravity.Y = value;

        var iterationsSlider = new Slider(2, 2, 15, 1);
        new LabeledElement<Slider>(controlsPanel, "Iterations", iterationsSlider);
        iterationsSlider.inputEvents.OnChange += (slider, value) => particleSystem.iterations = (int)value;

        window.globalEvents.MouseWheelScrolled += Zoom;
        window.globalEvents.MouseMoved += Pan;
    }

    private void Zoom(SFML.Window.MouseWheelScrollEventArgs e, Window window)
    {
        camera.Zoom(e.Delta);
    }

    private void Pan(SFML.Window.MouseMoveEventArgs e, Window window)
    {
        if (Events.IsMouseMiddleDown)
            camera.Pan(window.globalEvents.MouseDelta);
    }

    int lastParticle = -1;
    protected override void Update(float dt)
    {
        base.Update(dt);
    }

    protected override void FixedUpdate(float dt)
    {
        base.FixedUpdate(dt);
        if(window.events.IsMouseLeftDown)
        {
            var color = ColorUtils.NextFadeColor();
            
            for (var i = 0; i < 10000; i++) 
            {
                var mousePos = window.ScreenToWorld(Events.MousePosition);
                mousePos += (new Vector2(random.NextSingle(), random.NextSingle()) * 2 - new Vector2(1, 1)) * 50;

                if(lastParticle >= 20) 
                {
                    var particle = particleSystem.AddParticle(mousePos, new(0), color);
                    if (particle == null) continue;

                    particleSystem.AddLink(random.Next(lastParticle - 20, lastParticle), particle.Value, 0);
                    lastParticle = particle.Value;
                }
                else
                {
                    var particle = particleSystem.AddParticle(mousePos, new(0), color);
                    if (particle == null) continue;
                    lastParticle = particle.Value; 
                }
            }
        }
        particleSystem.Update(dt);
    }

    protected override void Draw(float dt)
    {
        base.Draw(dt);
        particleSystem.Draw(window);
    }
}
