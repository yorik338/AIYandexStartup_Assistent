# AI Assistant (Python)

This module drives the natural-language pipeline and forwards validated commands to the C# `JarvisCore` service.

## Running locally

1. Start the C# bridge from the `core` directory:

   ```bash
   dotnet run
   ```

2. Run the Python demo entry point from this folder:

   ```bash
   python main.py
   ```

By default the Python side talks to `http://127.0.0.1:5055`. If the C# service is running on a different host (for example from WSL or Docker), set the endpoint explicitly:

```bash
export JARVIS_CORE_ENDPOINT="http://host.docker.internal:5055"
python main.py
```

The script performs a bridge health check before sending commands and will log an error if it cannot reach the service.
