using ManagedDoom;
using ManagedDoom.Audio;
using ManagedDoom.UserInput;
using ManagedDoom.Video;

if (args.Length != 1 || !File.Exists(args[0]))
    throw new ArgumentException("Pass one existing IWAD path.");

var commandLine = new CommandLineArgs(new[]
{
    "-iwad", Path.GetFullPath(args[0]), "-warp", "1", "-skill", "3", "-nomonsters", "-nosound", "-nomusic",
});
var config = new Config
{
    video_highresolution = true,
    video_fullscreen = false,
};

using var content = new GameContent(commandLine);
var video = new VerificationVideo(config, content);
var input = new ForwardInput();
var doom = new Doom(
    commandLine,
    config,
    content,
    video,
    NullSound.GetInstance(),
    NullMusic.GetInstance(),
    input);

for (int tick = 0; tick < 8; tick++)
{
    doom.Update();
    video.Render(doom);
}

if (doom.State != DoomState.Game || doom.Game.State != GameState.Level)
    throw new InvalidOperationException($"Expected an active level, got {doom.State}/{doom.Game.State}.");

double initialX = doom.Game.World.ConsolePlayer.Mobj.X.ToDouble();
double initialY = doom.Game.World.ConsolePlayer.Mobj.Y.ToDouble();
input.Moving = true;
for (int tick = 0; tick < 70; tick++)
{
    doom.Update();
    video.Render(doom);
}

double finalX = doom.Game.World.ConsolePlayer.Mobj.X.ToDouble();
double finalY = doom.Game.World.ConsolePlayer.Mobj.Y.ToDouble();
double distance = Math.Sqrt(Math.Pow(finalX - initialX, 2) + Math.Pow(finalY - initialY, 2));
if (distance < 8d)
    throw new InvalidOperationException($"Gameplay input did not move the player ({distance:0.00} map units). ");

int distinctFrameBytes = video.Frame.Distinct().Count();
if (distinctFrameBytes < 16)
    throw new InvalidOperationException($"Rendered framebuffer was unexpectedly uniform ({distinctFrameBytes} byte values). ");

Console.WriteLine(
    $"Doom verifier passed: {doom.State}/{doom.Game.State}, moved {distance:0.00} units, " +
    $"rendered {video.Width}x{video.Height} with {distinctFrameBytes} distinct byte values.");

sealed class ForwardInput : IUserInput
{
    public bool Moving { get; set; }
    public void BuildTicCmd(TicCmd cmd)
    {
        cmd.Clear();
        if (Moving)
            cmd.ForwardMove = (sbyte)PlayerBehavior.ForwardMove[1];
    }

    public void Reset() { }
    public void GrabMouse() { }
    public void ReleaseMouse() { }
    public int MaxMouseSensitivity => 15;
    public int MouseSensitivity { get; set; } = 8;
}

sealed class VerificationVideo : IVideo
{
    private readonly Renderer _renderer;

    public VerificationVideo(Config config, GameContent content)
    {
        _renderer = new Renderer(config, content);
        Frame = new byte[4 * _renderer.Width * _renderer.Height];
    }

    public byte[] Frame { get; }
    public int Width => _renderer.Width;
    public int Height => _renderer.Height;
    public void Render(Doom doom) => _renderer.Render(doom, Frame);
    public void InitializeWipe() => _renderer.InitializeWipe();
    public bool HasFocus() => true;
    public int MaxWindowSize => _renderer.MaxWindowSize;
    public int WindowSize { get => _renderer.WindowSize; set => _renderer.WindowSize = value; }
    public bool DisplayMessage { get => _renderer.DisplayMessage; set => _renderer.DisplayMessage = value; }
    public int MaxGammaCorrectionLevel => _renderer.MaxGammaCorrectionLevel;
    public int GammaCorrectionLevel { get => _renderer.GammaCorrectionLevel; set => _renderer.GammaCorrectionLevel = value; }
    public int WipeBandCount => _renderer.WipeBandCount;
    public int WipeHeight => _renderer.WipeHeight;
}
