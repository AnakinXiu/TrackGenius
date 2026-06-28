# SerialPortWrapper Exception Analysis and Handling Plan

## Scope
This document analyzes potential runtime exceptions in `TrackGenius.Communication/SerialPortWrapper.cs` and proposes practical handling strategies.

## Potential Exceptions by Method

### 1) `OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)`

Potential exceptions:
- `ArgumentNullException`: `portName` is `null`.
- `ArgumentException`: invalid `portName` format or invalid serial settings (`baud`, `data`, `parity`, `stopBits`).
- `UnauthorizedAccessException`: port access denied or already used by another process.
- `IOException`: I/O failure while opening/reinitializing the port.
- `InvalidOperationException`: open/close state is invalid for the operation.
- `ObjectDisposedException`: using a disposed stream instance.

Handling solution:
- Validate all input arguments before creating `SerialPortStream`.
- Detach event handler and dispose old stream in a `try/finally` safe pattern.
- Catch specific exceptions and wrap/rethrow with context (`portName`, serial config) or surface via error callback/event.

---

### 2) `ClosePort()`

Potential exceptions:
- `IOException`: close operation fails due to underlying driver/state.
- `InvalidOperationException`: invalid port state transition.
- `ObjectDisposedException`: stream already disposed.

Handling solution:
- Guard against `null` and already-closed states.
- Catch `ObjectDisposedException` as a no-op during shutdown.
- Catch `IOException`/`InvalidOperationException` and report with context.

---

### 3) `SendBytes(byte[] sendData)`

Potential exceptions:
- `ArgumentNullException`: `sendData` is `null`.
- `InvalidOperationException`: stream not in a writable/open state.
- `IOException`: write failure.
- `ObjectDisposedException`: stream disposed.

Handling solution:
- Add explicit guard: `ArgumentNullException.ThrowIfNull(sendData)`.
- Check open and writable state before write.
- Catch and report `IOException`/`InvalidOperationException`/`ObjectDisposedException`.

---

### 4) `ReadBytes()`

Potential exceptions:
- `IOException`: read failure.
- `TimeoutException`: read timeout (depends on stream configuration).
- `InvalidOperationException`: stream state invalid for reading.
- `ObjectDisposedException`: stream disposed.

Handling solution:
- Keep current `CanRead` guard.
- Catch expected read exceptions and return empty payload when the error is recoverable.
- For non-recoverable errors, escalate through a dedicated error channel.

---

### 5) `SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)`

Potential exceptions:
- Exceptions from `ReadBytes()` propagation.
- Exceptions thrown by subscribers of `DataReceived`.

Handling solution:
- Wrap handler body in `try/catch` to avoid breaking event thread.
- Invoke subscribers in a protected block and report callback failures.

---

### 6) `Dispose()`

Potential exceptions:
- `IOException` or `InvalidOperationException` when closing.
- `ObjectDisposedException` if already disposed in race conditions.

Handling solution:
- Make dispose idempotent (`_disposed` flag).
- Unsubscribe event before disposing stream.
- Suppress only expected dispose-time exceptions; report unexpected ones.

## Recommended Handling Pattern

Use a single internal helper for serial operations to keep behavior consistent:

```csharp
private void ExecuteSerialAction(Action action, string operation)
{
    try
    {
        action();
    }
    catch (ObjectDisposedException ex)
    {
        throw new InvalidOperationException($"Serial port operation '{operation}' failed: stream is disposed.", ex);
    }
    catch (UnauthorizedAccessException ex)
    {
        throw new InvalidOperationException($"Serial port operation '{operation}' failed: access denied.", ex);
    }
    catch (IOException ex)
    {
        throw new InvalidOperationException($"Serial port operation '{operation}' failed: I/O error.", ex);
    }
    catch (TimeoutException ex)
    {
        throw new InvalidOperationException($"Serial port operation '{operation}' failed: timeout.", ex);
    }
}
```

And apply argument guards:

```csharp
ArgumentNullException.ThrowIfNull(sendData);
if (string.IsNullOrWhiteSpace(portName))
    throw new ArgumentException("Port name cannot be empty.", nameof(portName));
if (baud <= 0)
    throw new ArgumentOutOfRangeException(nameof(baud));
if (data <= 0)
    throw new ArgumentOutOfRangeException(nameof(data));
```

## Practical Notes

- Prefer catching specific exceptions only.
- Include contextual data in error messages (port name, operation).
- Keep `Dispose()` safe and repeatable.
- Avoid silent failures; either rethrow with context or report through a dedicated error event/logging mechanism.

## Optional Next Improvement (Code-level)

If you want stronger runtime resilience in production, add:
- a `SerialError` event for non-fatal runtime failures,
- an internal `_disposed` flag,
- unified exception wrapping for `OpenPort`, `ClosePort`, `SendBytes`, and `ReadBytes`.
