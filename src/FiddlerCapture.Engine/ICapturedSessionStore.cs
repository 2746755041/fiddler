using System.Collections.Generic;

namespace FiddlerCapture.Engine;

public interface ICapturedSessionStore
{
    int Count { get; }

    void Add(CapturedSessionRecord session);

    IReadOnlyList<CapturedSessionRecord> Query(CaptureQuery query);

    void Clear();
}
