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
Source file: hello.asm
Output file (default: rom.bin):
Assembled hello.asm -> rom.bin (37 bytes)
```

Assembly errors are reported with a line number, e.g. `ASSEMBLY ERROR: line 7:
invalid register '$32'`.

## Syntax

### Comments

`;` starts a comment. Everything after it on the line is ignored.

```asm
; this is a comment
LDIb $03, 5 ; so is this
```

### Labels

A label is an identifier followed by a `:` and resolves to the address of the
byte it precedes. Labels are case-insensitive and can be referenced by any
instruction that takes an address (`JMP`, `CALL`, `JNZ`, `JZ`) or by a data
directive.

```asm
start:
    JMP start      ; jumps to 0x00
```

Labels may appear on their own line or in front of an instruction:

```asm
loop:
    SUB $03, $01, $03
    JNZ loop, $03
```

### Registers

Registers are written as `$` followed by two hex digits, or by an alias.

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

### Values

Values can be decimal, hex (`0x` prefix) or binary (`0b` prefix).

```asm
LDIb $04, 255    ; decimal
LDIb $04, 0xFF   ; hex
LDIb $04, 0b11111111 ; binary
```

Addresses may be a value or a label.

### Directives

| Directive | Description |
|-----------|-------------|
| `.org <addr>` | Sets the current output position. Gaps are filled with zero bytes. |
| `.byte <v>[, <v>...]` | Emits one 8-bit value per operand. |
| `.word <v>[, <v>...]` | Emits one 16-bit little-endian value per operand. |
| `.dword <v>[, <v>...]` | Emits one 32-bit little-endian value per operand. |

```asm
.org 0x200          ; interrupt table
.dword int_handler  ; interrupt 0 -> handler
.org 0x300
int_handler:
    RET
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
    .byte 0xFF           ; halt (unknown instruction)
add_one:
    LDIb $06, 1
    ADD $05, $06, $05
    RET
```

The ISA has no `HALT` instruction, so the example stops the simulator by
executing `0xFF`, an unknown opcode, which raises a CPU error.

## Limits

The maximum ROM size is 64 kilobytes (the simulator's default RAM size).
`.org` addresses and the final image must not exceed this.
