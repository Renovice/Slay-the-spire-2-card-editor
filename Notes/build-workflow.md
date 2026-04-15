# Build Workflow

- After any code change, always build and deploy the DLL to BOTH locations:
  1. `built cfiles\card_editor.dll`
  2. `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card_editor\card_editor.dll`
- Build command: `cd mods\card_editor; dotnet build card_editor.csproj -c Release`
- DLL output: `mods\card_editor\build\net9.0\card_editor.dll`
- Do NOT rebuild .pck unless scene files changed (and use Godot 4.5.1, NOT 4.6.1)
- Godot path: `C:\Users\Bartek\OneDrive\Skrivebord\Godot_v4.6.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe`
