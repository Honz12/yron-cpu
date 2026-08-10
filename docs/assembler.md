# Assembler

The assembler converts assembly source files into a raw ROM image that can be
run by the simulator.

## Usage

Start the app and select `2. Assembler`, then enter the source file and the
output file (defaults to `rom.bin`).

```
What would you like to run?
  1. Simulator
  2. Assembler
  q. Quit
> 2
Source file: hello.yrn
Output file (default: rom.bin):
Assembled hello.yrn -> rom.bin (37 bytes)
```

The assembler (and simulator) can also be driven non-interactively from the
command line:

```
cpu asm <source.yrn> [output.bin]
cpu sim <rom.bin>
```

## Errors

Assembly errors are reported with the source file and line number, e.g.
`ASSEMBLY ERROR: hello.yrn:7: invalid register '$32'`. Included files report
the file they came from.

## Preprocessor

The assembler runs a small textual preprocessor before parsing. `%include`
inserts another file, `%define` creates an object-like text macro.

### `%define`

`%define <NAME> <text>` registers a macro. Every occurrence of `NAME` in
subsequent lines (including inside included files) is replaced by `<text>`.

```asm
%define SCREEN 0x3000
%define MSG "Hello; world"
%define TWICE 2 * 8

    LDIw $04, SCREEN + 4
    LDIb $06, TWICE          ; -> 16
```

Rules:

- Macro names are case-sensitive identifiers and are matched on word
  boundaries. Macro values are **not** expanded inside string or character
  literals.
- Macro values are rescanned, so macros may reference other macros:
  `%define A B` + `%define B 7` makes `A` expand to `7`. A macro that
  (directly or indirectly) references itself is left unexpanded rather than
  looping forever.
- Redefining a macro replaces the previous definition.

### `%include`

`%include "file.yrn"` textually inserts another source file at that point.
Paths are resolved relative to the directory of the file containing the
`%include` (falling back to the current directory for absolute/relative
searches).

Every file is included **at most once**: if the same file is included a second
time (directly or through another include), the second include is skipped.
This also prevents include cycles.

```asm
%include "defines.yrn"   ; inserted here
%include "defines.yrn"   ; skipped, already included
```

## Syntax

### Comments

`;` starts a comment. Everything after it on the line is ignored. A `;` inside
a string or character literal is part of the literal, not a comment.

```asm
; this is a comment
LDIb $03, 5 ; so is this
%ascii "a;b" ; the first ';' is data
```

### Labels

A label is an identifier followed by a `:` and resolves to the address of the
byte it precedes. Labels are **case-sensitive** and can be referenced by any
instruction that takes an address (`JMP`, `CALL`, `JNZ`, `JZ`) or by a data
directive.

```asm
start:
    JMP start      ; jumps to 0x00
```

Labels may appear on their own line or in front of an instruction:

```asm
loop:
    SUB $03, $0A, $03
    JNZ loop, $03
```

### Local labels

A local label is written `.name:` and binds to the most recent global label in
the same file (NASM-style). This lets a routine reuse common names such as
`.loop` or `.done` without collisions.

```asm
count_down:
    LDIb $0A, 3
.loop:
    SUB $0A, $0B, $0A
    JNZ .loop, $0A
    JMP .done              ; forward reference
.done:
    RET
```

Rules:

- `.name:` binds to the nearest preceding global label **in the same file**.
  The same local name may be reused under a different global label, even in
  the same file.
- An included file starts with an empty local scope: its local labels never
  bind to, or collide with, local labels in the including file.
- Local labels are referenced as `.name` (e.g. in address operands or data
  directives) and are case-sensitive like global labels.
- Using a local label — defining or referencing it — before any global label
  is an error.

### Registers

Registers are written as `$` followed by two hex digits, or by an alias.
Register aliases are case-insensitive.

| Syntax      | Register |
|-------------|----------|
| `$00`-`$1F` | `$00`-`$1F` |
| `$pc` / `${pc}`   | `$00` (program counter) |
| `$intr` / `${intr}` | `$01` (interrupt reason) |
| `$sp` / `${sp}`   | `$02` (stack pointer) |

```asm
LDIb $0A, 7
MOV $0B, $0A
MOV ${sp}, $03
```

Registers outside `$00`-`$1F` are rejected at assembly time.

### Values and expressions

Operands may be decimal, hex (`0x` prefix), binary (`0b` prefix), a character
literal (`'A'`, `'\n'`, `'\t'`, `'\r'`, `'\0'`, `'\\'`, `'\''`, `'\"'`), a
label, or an arbitrary arithmetic expression.

```asm
LDIb $04, 255            ; decimal
LDIb $04, 0xFF           ; hex
LDIb $04, 0b11111111     ; binary
LDIb $04, 'A'            ; character literal -> 0x41
LDIw $04, (2 + 3) * 4    ; expression -> 20
LDIb $05, 1 << 8         ; shift -> 256
```

Supported operators, lowest to highest precedence: `|`, `^`, `&`, `<<` `>>`,
`+` `-`, `*` `/` `%`, unary `-` `~` `+`. Parentheses group. Division by zero
is an error. Labels are resolved case-sensitively.

Operands are separated by commas (the separator inside a string literal is
ignored), so expressions may contain spaces:

```asm
SUB $03, $0A, $03
LDIb $04, (BASE + OFFSET) & 0xFF
```

### Directives

Directives start with `%` and are case-insensitive.

| Directive | Description |
|-----------|-------------|
| `%org <addr>` | Sets the current output position. Gaps are filled with zero bytes. Moving backwards is an error. |
| `%byte <v>[, <v>...]` | Emits one 8-bit value per operand. String operands emit each character. |
| `%word <v>[, <v>...]` | Emits one 16-bit little-endian value per operand. |
| `%dword <v>[, <v>...]` | Emits one 32-bit little-endian value per operand. |
| `%ascii "<str>"[, <v>...]` | Emits raw bytes (strings or 8-bit values), no terminator. |
| `%asciz "<str>"[, <v>...]` | Same as `%ascii`, then emits a trailing `0` byte. |
| `%align <n>[, <fill>]` | Advances to the next multiple of `<n>` (default fill byte `0`). |

```asm
%org 0x200          ; interrupt table
%dword int_handler  ; interrupt 0 -> handler
%org 0x300
int_handler:
    RET
```

String literals support the same escapes as character literals
(`\n`, `\t`, `\r`, `\0`, `\\`, `\'`, `\"`):

```asm
%asciz "Hello, world!"   ; bytes + trailing 0x00
%ascii "a;b"             ; bytes only
%byte "AB", 0x0D, 0x0A
%align 4                 ; pad to next 4-byte boundary
```

### PUSH / POP sizes

`PUSH` and `POP` take a size operand. Both the numeric encoding and the keyword
forms are accepted:

| Size     | Encoding | Bytes |
|----------|----------|-------|
| `BYTE`   | `0`      | 1     |
| `WORD`   | `1`      | 2     |
| `DWORD`  | `2`      | 4     |

```asm
PUSH $05, WORD
POP $06, DWORD
PUSH $05, 1          ; same as WORD
```

## Instructions

Mnemonics are case-insensitive. See `isa.md` for the full instruction set.

| Mnemonic | Operands |
|----------|----------|
| NOP | - |
| INT | Reason |
| CALL | Address |
| RET | - |
| LDIb / LDIw / LDId | Register, Value |
| LDb / LDw / LDd | Register, Address register |
| STb / STw / STd | Register, Address register |
| MOV | Dest register, Source register |
| ADD, SUB, MUL, DIV, MOD, AND, NAND, OR, NOR, XOR, EQ, GT, GTE, LT, LTE | A register, B register, Dest register |
| JMP | Address |
| JNZ / JZ | Address, Condition register |
| PUSH | Value register, Size |
| POP | Dest register, Size |

## Example

```asm
; Call a routine that increments $05 by 1, then halt the simulator
    LDIb $05, 42
    CALL add_one         ; $05 = 42 + 1 = 43
    %byte 0xFF           ; halt (unknown instruction)
add_one:
    LDIb $06, 1
    ADD $05, $06, $05
    RET
```

The ISA has no `HALT` instruction, so the example stops the simulator by
executing `0xFF`, an unknown opcode, which raises a CPU error.

## Limits

The maximum ROM size is 64 kilobytes (the simulator's default RAM size).
`%org` addresses, `%align` targets and the final image must not exceed this.
