# Linker

The linker lets you build a program from several already-compiled libraries.
`cpu asm` produces a `.yrl` link file for each source, `cpu link` merges them
and relocates their addresses, and `cpu build` turns the final link file into a
raw ROM image.

## Usage

```
cpu asm <source.yrn> <output.yrl>      ; compile a library
cpu asm <source.yrn> -l                ; same, output defaults to <source>.yrl
cpu link <a.yrl> <b.yrl> [more...] [-o output.yrl]   ; link libraries (default: linked.yrl)
cpu build <final.yrl> [output.bin]     ; produce the ROM image (default: rom.bin)
```

The same steps are available from the interactive menu (options `3. Linker`
and `4. Builder`).

```
cpu asm lib.yrn lib.yrl
cpu asm main.yrn main.yrl
cpu link lib.yrl main.yrl -o program.yrl
cpu build program.yrl rom.bin
cpu sim rom.bin
```

## Link files (.yrl)

A `.yrl` file is a compiled library: a binary file with the magic header
`YRL\0`, followed by:

1. **Symbol table** — exported labels. Each entry is a null-terminated string
   (the label name) followed by a 32-bit little-endian DWORD (the address).
2. **Reference table** — every 32-bit address operand that refers to a label,
   whether defined in this file or `%extern`. Each entry is a null-terminated
   name followed by a DWORD giving the byte offset *inside the binary* where
   the address must be patched. For a label defined in the same file the DWORD
   at that offset holds the file-relative address; for an `%extern` label it
   is a placeholder (zero).
3. **Binary** — the compiled bytes.

Both tables end with a single `\0` byte (an empty name), and no DWORD follows
the final terminator.

```
'Y' 'R' 'L' 0x00
<name>\0 <dword addr>  <name>\0 <dword addr> ... \0
<name>\0 <dword off>   <name>\0 <dword off>  ... \0
<compiled bytes>
```

All addresses in a `.yrl` are **relative to the file's own start** (`0x00`).
The linker relocates them to their final positions in the combined image.

## Library assembly

Assembling to a `.yrl` differs from a plain ROM build:

- Every defined label (global and local) is exported in the symbol table.
- `%org` simply pads the binary with zero bytes, so offsets stay relative to
  the file's start. Code that starts with a large `%org` wastes space when
  linked — use `%org` sparingly in libraries.
- Labels you expect another file to provide must be declared with `%extern`.
- To expose a label under an extra name, use `%aliasl <name> <label>`; the
  alias is exported alongside the real label. This is useful for defining
  "public API" names that live in a different source file, e.g.
  `%aliasl lib_putc lib_display_device_putc`.

`%extern` is only allowed when the output is a `.yrl` file. External symbols
may only be used where a full 32-bit address is emitted (e.g. `CALL`, `JMP`,
`LDId`, `%dword`), and must appear by themselves in the operand.

References to labels **defined in the same library** are recorded too: any
32-bit address operand (instruction or `%dword`) that names a label — global
or local — is relocated automatically when the library is linked, so code can
freely `CALL`, `JMP` or `LDId` its own labels. Label operands in **byte- or
word-sized** positions (e.g. `%byte label`) are *not* relocatable: they are
encoded as file-relative addresses and are only correct in a single-file
plain build.

```asm
; printf.yrn
%extern puts_char          ; provided by another library

print_hex:
    push $10, DWORD
    call puts_char
    pop $10, DWORD
    ret
```

## Linking

`cpu link` reads every input, validates the `YRL\0` header, and concatenates
the binaries in the order given. The first file is placed at `0x00`, the next
right after it, and so on. It then:

- Rebases every exported symbol by adding its file's base address.
- Patches every reference. A reference to a label **defined in the same file**
  is relocated by adding that file's base address to the value already encoded
  at the recorded offset (local labels like `.loop` are included). A reference
  to an **`%extern`** label is resolved against the combined symbol table and
  overwrites the placeholder.
- Fails on **duplicate symbols** (two files exporting the same name) and on
  **unresolved references** (a name no input file exports).
- Errors if the combined image exceeds the 64 KB ROM limit.

The result is a new `.yrl` whose reference table is empty; its binary is the
final relocated image.

## Building

`cpu build` refuses to run if any references remain unresolved (link first),
then writes just the binary — the tables are stripped — to the output file,
ready for the simulator.

## Example

```asm
; lib.yrn
%extern puts

hello:
    %asciz "hi"
lib_main:
    ldid $10, hello
    call puts
    ret
```

```asm
; main.yrn
%extern lib_main

entry:
    call lib_main
    jmp entry

puts:
    ret
```

```
cpu asm lib.yrn lib.yrl
cpu asm main.yrn main.yrl
cpu link lib.yrl main.yrl -o prog.yrl
cpu build prog.yrl
cpu sim rom.bin
```
