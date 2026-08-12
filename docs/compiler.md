# Compiler (yrC)

The yrC compiler translates C-like source files (`.yrc`) into yron assembly
text (`.yrn`), which can then be assembled into a ROM image and run in the
simulator. It is a small, self-contained compiler: lexer, recursive-descent
parser, resolver (types/checks) and a direct code generator. There is no
intermediate representation and no optimizer; code is emitted straight from
the AST.

## Usage

Start the app and select `5. Compiler`, then enter the source file and the
output file (defaults to `<source>.yrn`).

```
What would you like to run?
  1. Simulator
  2. Assembler
  3. Linker
  4. Builder
  5. Compiler
  6. Test headless
  q. Quit
> 5
Source file: examples/fib.yrc
Output file (default: examples/fib.yrn):
Compiled examples/fib.yrc -> examples/fib.yrn
```

Everything can also be driven non-interactively from the command line:

```
cpu cc <source.yrc> [output.yrn] [--build]
```

`--build` chains the assembler, producing a ROM image in place of the
output `.yrn`:

```
cpu cc examples/fib.yrc --build        ; -> examples/fib.yrn + rom.bin
cpu cc examples/fib.yrc out/fib.yrn    ; -> out/fib.yrn
```

The generated assembly is assembled with the same assembler used for
hand-written code and can be inspected or run directly:

```
cpu asm examples/fib.yrn fib.bin
cpu test fib.bin
cpu test fib.bin --disasm             ; print disassembly of the whole ROM
cpu test fib.bin --trace 200          ; step through the first 200 instructions
```

Compile errors are reported with the source file and line:column and a
message, e.g. `COMPILE ERROR: examples/ops.yrc:24:10: expected ';', got 'msg'`.

## Language

yrC is a small C-like language. Programs are a list of global declarations:
struct definitions, functions, and global variables. Execution starts at a
function named `main`; its return value ends up in `$0F`.

### Types

| Type   | Size (bytes) | Signed |
|--------|:----:|:------:|
| `u8`   | 1    | no     |
| `u16`  | 2    | no     |
| `u32`  | 4    | no     |
| `i8`   | 1    | yes    |
| `i16`  | 2    | yes    |
| `i32`  | 4    | yes    |
| `char` | 1    | yes    (alias for `i8`) |
| `void` | 0    | —      (function return type only) |

Compound types:

- `T*` — pointer to `T` (always 4 bytes, unaligned loads allowed).
- `T[N]` — array of `N` elements of `T` (compile-time constant `N`).
- `struct Name { ... }` — structs are passed by reference only, and cannot be
  assigned, compared or returned by value. Structs have natural alignment.

Integer literals are decimal, `0x` hex, `0b` binary, or a character literal
(`'A'`, `'\n'`, ...). Unsuffixed positive literals type as `u32`; a negative
literal (unary minus applied to a positive literal) types as `i32`. Narrowing
assignments require an explicit cast, except for constant literals that fit the
target type.

### Global and local declarations

```c
u32 global_counter = 0;          // global variable (zero-initialized)
u8 banner[] = "hello";           // global array initialized from a string

u32 add(u32 a, u32 b)            // function
{
    u32 local = a + b;           // locals live in the function's frame
    return local;
}
```

Local `u8 name[N]` arrays can be initialized from a string literal, which
copies the bytes (including the trailing NUL) from ROM into the array.

### Statements

- `if (cond) stmt` / `if (cond) stmt else stmt`
- `while (cond) stmt`
- `do stmt while (cond);`
- `for (init; cond; step) stmt` — `init` and `step` are expressions
  (declarations in the `for` header are not supported yet)
- `return [expr];`
- `break;` / `continue;` — inside any loop
- `{ ... }` blocks
- `type name [= init];` local declarations
- `expr;` expression statements

### Expressions

- Arithmetic: `+ - * / %` (with integer promotion; unsigned vs signed
  division/modulo handled automatically)
- Bitwise/logical: `& | ^ ~ << >> && || !`
- Comparisons: `== != < <= > >=` (signed and unsigned)
- Assignments: `=`
- Unary: `- ~ ! &` (address-of) and `*` (dereference)
- Indexing: `arr[i]`, pointer arithmetic `p + i`
- Struct member access: `s.field` and `p->field` (same as `(*p).field`)
- Casts: `(type) expr`
- Calls: `name(arg1, arg2, ...)`

Short-circuit evaluation: `&&` and `||` evaluate the right operand only when
needed. Comparison results are `u32` `0`/`1`.

### Functions

- Up to 4 parameters are passed in registers `$10`–`$13`; the compiler copies
  them to the callee's stack frame on entry. Parameters beyond the fourth are
  pushed on the stack (in reverse order) and read from `[fp+8]`, `[fp+12]`, ...
- A function may call itself (the frame pointer and return address are saved
  on the stack, so recursion works).
- The return value is placed in `$0F`; calling code copies it to a scratch
  register before using it, because `$0F` is not preserved across calls.
- A function with no `return` falls off the end and returns whatever is in
  `$0F`.

## ABI

The generated code follows a fixed register convention (see also `isa.md`):

| Register | Role |
|:---:|---|
| `$02` | stack pointer |
| `$0E` | `T0` — primary result register |
| `$0F` | return value register |
| `$03`–`$0D` | `T1`–`T11` — scratch, caller-saved, clobbered by calls |
| `$10`–`$13` | `A0`–`A3` — argument registers (caller-saved) |
| `$1F` | frame pointer (callee-saved) |

Calling convention: the caller leaves up to four arguments in `$10`–`$13` and
issues `call`; the callee's prologue saves the frame pointer, sets up the new
frame and copies register arguments into frame slots at `[fp-4]`, `[fp-8]`,
etc. The epilogue restores the frame pointer and returns. The stack grows
downwards from the top of RAM.

Locals are laid out below the frame pointer so that each object lies entirely
below `fp`: the last declared local ends up at `[fp-4]`. Arrays and structs
occupy one contiguous region and are accessed via their base address.

## Generated code

- Labels: functions as `f__name`, globals as `g__name`, string constants as
  `s__0`, `s__1`, ...
- Program shape:
  ```
  _start:
      call f__main
  __halt:
      jmp __halt
  ```
- Memory access is little-endian. `u8`/`i8` and `u16`/`i16` are loaded with
  `ldb`/`ldw` and sign-extended (`__sext8`/`__sext16`) for signed types.
- Shifts are emulated (the ISA has no shift instruction): constant shifts use
  `mul`/`div` by `1 << n`; variable shifts call the `__shl`/`__shr`/`__sar`
  helper loops.
- Signed `/` and `%` call the `__sdiv`/`__smod` helpers, which negate
  operands as needed and divide the absolute values.
- Signed comparisons are done by XORing both operands with `0x80000000` and
  using the unsigned comparison instructions.
- Helpers are only emitted if used: `__memcpy`, `__shl`, `__shr`, `__sar`,
  `__sext8`, `__sext16`, `__sdiv`, `__smod`.

## Examples

Small example programs live in `examples/`:

| File | What it exercises | Return value |
|---|---|---|
| `fib.yrc` | recursive calls, register args, `if/else` | `55` (fib(10)) |
| `sieve.yrc` | nested loops, arrays, global state | `46` (primes < 200) |
| `structs.yrc` | structs, pointers, `->`, arrays of structs | `20` |
| `ops.yrc` | arithmetic, bitwise, shifts, casts, signed div/mod, chars | `42` |
| `flows.yrc` | `do`/`while`/`for`, `break`/`continue`, string init, `__sar` | `317` |

Build and run any of them with:

```
cpu cc examples/fib.yrc examples/fib.yrn
cpu asm examples/fib.yrn fib.bin
cpu test fib.bin
```
