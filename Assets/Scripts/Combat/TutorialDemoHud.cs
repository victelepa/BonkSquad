using UnityEngine;
using UnityEngine.UIElements;

internal sealed class TutorialDemoHud
{
    public enum Sequence { None, LowSpeed, HighSpeed, Dash }

    private readonly VisualElement panel;
    private readonly Label caption;
    private readonly Image image;
    private readonly IVisualElementScheduledItem playback;
    private TutorialGif gif;
    private Texture2D texture;
    private Sequence sequence;
    private int previousFrame;
    private double startedAt;

    public TutorialDemoHud(VisualElement root)
    {
        panel = root.Q("tutorial-demo");
        caption = panel.Q<Label>("tutorial-demo-caption");
        image = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        image.style.flexGrow = 1;
        panel.Q("tutorial-demo-frame").Add(image);
        playback = panel.schedule.Execute(Refresh).Every(16);
        playback.Pause();
    }

    public void Show(Sequence next)
    {
        Stop();
        sequence = next;
        panel.EnableInClassList("hidden", next == Sequence.None);
        panel.EnableInClassList("tutorial-demo-with-dash", next == Sequence.Dash);
        if (next == Sequence.None) return;

        string name = next switch
        {
            Sequence.LowSpeed => "Practice-3-Ice",
            Sequence.HighSpeed => "Practice-4-Cosmos",
            _ => "Practice-7-Dash"
        };
        var asset = Resources.Load<TextAsset>("CombatUI/Demos/" + name + ".gif");
        gif = new TutorialGif(asset.bytes);
        texture = new Texture2D(gif.Width, gif.Height, TextureFormat.RGB24, false) { name = name };
        image.image = texture;
        startedAt = Time.unscaledTimeAsDouble;
        previousFrame = -1;
        Refresh();
        playback.Resume();
    }

    public void Stop()
    {
        playback.Pause();
        image.image = null;
        if (texture != null) Object.Destroy(texture);
        texture = null;
        gif = null;
    }

    private void Refresh()
    {
        int elapsed = (int)((Time.unscaledTimeAsDouble - startedAt) * 1000) % gif.Duration;
        int frame = System.Array.BinarySearch(gif.EndTimes, elapsed + 1);
        if (frame < 0) frame = ~frame;
        if (frame == previousFrame) return;
        previousFrame = frame;
        texture.LoadRawTextureData(gif.Frames[frame]);
        texture.Apply(false);
        bool secondClip = frame >= gif.Frames.Length / 2;
        caption.text = sequence switch
        {
            Sequence.LowSpeed => secondClip ? "Low-speed bounce" : "Ice trail",
            Sequence.HighSpeed => secondClip ? "High-speed breakthrough" : "Cosmos trail",
            _ => "Dash toward an enemy"
        };
    }
}
