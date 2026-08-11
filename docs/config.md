# Simulator config (yconf.json)

A config file lets you launch the simulator without the interactive device
menu or the `[S]` step-mode prompt.

## Usage

```
cpu new yconf          ; create a default yconf.json in the current directory
cpu sim <config.json>  ; run the simulator using a config file
cpu sim <rom.bin>      ; plain ROM run still works (device menu, [S] prompt)
```

Running `cpu sim` from the interactive menu also accepts a `.json` path.

## Format

```json
{
  "rom": "rom.bin",
  "devices": {
    "display": false,
    "keyboard": false
  },
  "ipd": 50000,
  "stepMode": false,
  "fullscreen": false
}
```

| Property   | Type   | Meaning |
|------------|--------|---------|
| `rom`      | string | Path of the program to run (default `rom.bin`). |
| `devices`  | object | Boolean map of devices to enable. Valid keys: `display`, `keyboard`. Unknown keys are an error. |
| `ipd`      | number | Instructions Per Draw — how many CPU instructions run per frame (default `50000`, must be at least 1). |
| `stepMode` | bool   | Start in step mode (`F1` steps one instruction, `F2` toggles immediate input mode). |
| `fullscreen` | bool | Launch the window in fullscreen (can still toggle with `F11`). |

`cpu new yconf` writes the file above: every device off, `stepMode` and
`fullscreen` off, and `ipd` at its default.
