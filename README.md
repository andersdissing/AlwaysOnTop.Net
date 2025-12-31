# Always On Top

A lightweight Windows system tray application that lets you pin any window to stay always on top of other windows.

## Features

- **System tray icon** - Runs quietly in the notification area
- **Window list** - Right-click the tray icon to see all open windows
- **Toggle always-on-top** - Click any window to pin/unpin it
- **Global hotkey** - Press `Shift+Ctrl+Alt+T` to toggle the active window
- **Visual feedback** - Checkmarks indicate pinned windows, balloon notifications confirm changes

## Requirements

- Windows 10/11
- .NET 8.0 Runtime

## Usage

### Run from source

```bash
dotnet run
```

### Build executable

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

### Using the app

1. Launch the application - a blue pin icon appears in the system tray
2. Right-click the icon to see a list of all open windows
3. Click a window name to toggle its always-on-top state
4. Or press `Shift+Ctrl+Alt+T` to pin/unpin the currently focused window
5. Select "Exit" from the menu to close the application

## License

MIT
