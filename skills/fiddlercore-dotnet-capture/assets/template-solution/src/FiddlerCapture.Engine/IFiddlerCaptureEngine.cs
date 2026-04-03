using System.Threading;
using System.Threading.Tasks;

namespace FiddlerCapture.Engine;

public interface IFiddlerCaptureEngine
{
    CaptureEngineState GetState();

    ValueTask StartCaptureAsync(CancellationToken cancellationToken = default);

    ValueTask StopCaptureAsync(CancellationToken cancellationToken = default);
}
